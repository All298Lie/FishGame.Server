using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BattleArenaServer
{
    /// <summary>
    /// 배틀아레나 게임 서버
    ///
    /// ═══════════════════════════════════════════════
    /// [3주차 — TICKET-002]
    /// "클라팀이 접속 테스트를 원합니다"
    ///
    /// 오늘 하는 것:
    ///   - 서버 열기 (포트 7777)
    ///   - 클라이언트 접속 받기
    ///   - 로그인 처리 (닉네임 받고 환영 메시지 보내기)
    ///   - 입장/퇴장 알림 브로드캐스트
    ///
    /// 다음 주에 할 것:
    ///   - 100명 동시 접속 처리 (async 개선)
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class GameServer
    {
        public static GameServer Instance { get; private set; } = null!;

        private const int PORT = 7777;

        private Socket? _listenSocket;
        private int _nextPlayerId = 0;

        private GameLoop? _gameLoop;
        private readonly System.Diagnostics.Stopwatch _uptime = new();

        // ── 8주차 추가 ──
        private readonly SaveQueue _saveQueue = new SaveQueue();

        /// <summary>게임 루프 (ClientSession 이 판 정보를 물어볼 때 씀)</summary>
        public GameLoop Loop => _gameLoop!;

        // 접속 중인 클라이언트 목록
        // ConcurrentDictionary : 여러 쓰레드가 동시에 접근해도 안전한 사전
        private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();

        public GameServer()
        {
            Instance = this;
        }

        // ═══════════════════════════════════════
        // 서버 시작
        // ═══════════════════════════════════════
        public async Task StartAsync()
        {
            PrintBanner();
            _uptime.Start();

            // ── 8주차 : DB 확인 → 저장 작업자 시작 ──
            //   DB 가 없어도 서버는 뜹니다. 게임은 되고 저장만 안 됩니다.
            await Db.CheckAsync();
            _ = _saveQueue.RunAsync();

            // ── 1단계 : 소켓 만들기 ──
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,  // IPv4
                SocketType.Stream,           // TCP는 Stream
                ProtocolType.Tcp);

            // ── 2단계 : 주소에 묶기 (Bind) ──
            // 0.0.0.0 = 이 컴퓨터의 모든 네트워크로 들어오는 연결을 받겠다
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            _listenSocket.Bind(endPoint);

            // ── 3단계 : 대기 시작 (Listen) ──
            // 100 = 동시에 대기시킬 수 있는 접속 요청 수
            _listenSocket.Listen(100);

            Console.WriteLine($"[TCP] 포트 {PORT}번에서 접속을 기다립니다...");

            // ── 5주차 추가 : UDP 서버도 같이 켭니다 ──
            //
            //   TCP 포트 7777 — 로그인, 채팅 (반드시 도착해야 함)
            //   UDP 포트 7778 — 위치        (최신 값만 중요)
            //
            //   await를 붙이지 않습니다.
            //   붙이면 UDP 루프에서 안 빠져나와서
            //   아래 Accept로 못 갑니다.
            // ── 6주차 : UDP 입출력과 게임 루프를 따로 띄웁니다 ──
            //
            //   UdpServer  받아서 큐에 넣기만
            //   GameLoop   큐를 꺼내 판정하고 뿌리기
            //
            //   받는 쪽과 판정하는 쪽을 떼어놓는 것이
            //   오늘 배우는 구조입니다.
            var udpServer = new UdpServer();
            _ = udpServer.StartAsync();

            _gameLoop = new GameLoop(udpServer, _saveQueue);
            _ = _gameLoop.RunAsync();

            _ = StatsLoopAsync();
            _ = ConsoleCommandLoopAsync();

            // ── 4단계 : 접속 받기 (Accept) 무한 반복 ──
            while (true)
            {
                // 클라이언트가 접속할 때까지 여기서 대기
                Socket clientSocket = await _listenSocket.AcceptAsync();

                // 플레이어 ID 발급
                int playerId = Interlocked.Increment(ref _nextPlayerId);

                // 세션 만들고 목록에 추가
                ClientSession session = new ClientSession(clientSocket, playerId);
                _sessions.TryAdd(playerId, session);

                Console.WriteLine($"[현재 접속자] {_sessions.Count}명");

                // 이 클라이언트 전용 처리를 시작 (기다리지 않고 바로 다음 접속 받으러 감)
                _ = session.RunAsync();
            }
        }

        // ═══════════════════════════════════════
        // 상태 출력 — 2초마다
        //
        //   거부   검증에 걸린 입력 (위조 · 과도한 거리 · 지난 번호)
        //   폐기   줄이 꽉 차서 버린 입력          ← 오늘의 새 지표
        //
        //   정상 클라이언트만 붙어 있으면 폐기는 0에 가깝습니다.
        //   속도핵(예제 G 6번)을 돌리면 폐기 숫자가 수십씩 오릅니다.
        //   "보내긴 보냈지만 서버가 안 써줬다" 는 뜻입니다.
        // ═══════════════════════════════════════
        private async Task StatsLoopAsync()
        {
            while (true)
            {
                await Task.Delay(2000);
                if (_gameLoop == null) continue;

                Console.WriteLine(
                    $"[통계] t={_uptime.Elapsed.TotalSeconds,5:F0}s | " +
                    $"접속 {_sessions.Count,3} | " +
                    $"틱 {_gameLoop.Tick,6} | " +
                    $"입력 {_gameLoop.InputCount,7} | " +
                    $"거부 {_gameLoop.RejectCount,6} | " +
                    $"폐기 {_gameLoop.DropCount,6} | " +
                    $"아이템 {_gameLoop.ItemCount,2} | " +
                    $"{_gameLoop.RoundNo}판 {(_gameLoop.InRound ? "진행" : "휴식")} {_gameLoop.RemainSeconds,2}s | " +
                    $"저장 {_saveQueue.Saved} 실패 {_saveQueue.Failed} 대기 {_saveQueue.Pending}");
            }
        }

        // ═══════════════════════════════════════
        // [8주차] 서버 콘솔 명령
        //
        //   rank   랭킹 TOP 10 (DB 에서 읽음)
        //   who    지금 접속자
        // ═══════════════════════════════════════
        private async Task ConsoleCommandLoopAsync()
        {
            await Task.Yield();

            while (true)
            {
                string? line = await Task.Run(() => Console.ReadLine());
                if (line == null) return;            // 입력이 없는 환경(서비스 등)

                switch (line.Trim().ToLower())
                {
                    case "rank":
                        await PrintRankingAsync();
                        break;

                    case "who":
                        foreach (var s in GetSessions())
                            Console.WriteLine($"  접속번호 {s.PlayerId,3} · DB id {s.DbPlayerId,4} · {s.Nickname} · {s.Score}점");
                        break;

                    case "":
                        break;

                    default:
                        Console.WriteLine("  명령 : rank (랭킹 TOP 10) · who (접속자)");
                        break;
                }
            }
        }

        private static async Task PrintRankingAsync()
        {
            if (!Db.IsAvailable)
            {
                Console.WriteLine("  DB 없이 실행 중이라 랭킹이 없습니다.");
                return;
            }

            try
            {
                var top = await RankingRepository.TopAsync(10);

                Console.WriteLine("  ── 랭킹 TOP 10 (최고 점수) ──");
                for (int i = 0; i < top.Count; i++)
                    Console.WriteLine($"  {i + 1,2}위  {top[i].Nickname,-10} {top[i].BestScore,4}점  ({top[i].GamesPlayed}판)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  랭킹을 읽지 못했습니다 : {ex.Message}");
            }
        }

        // ═══════════════════════════════════════
        // 세션 관리
        // ═══════════════════════════════════════
        public void RemoveSession(int playerId)
        {
            _sessions.TryRemove(playerId, out _);
            Console.WriteLine($"[현재 접속자] {_sessions.Count}명");
        }

        // ═══════════════════════════════════════
        // [5주차 추가] UDP 서버가 쓰는 조회 함수
        // ═══════════════════════════════════════

        /// <summary>플레이어 ID로 세션 찾기</summary>
        public ClientSession? FindSession(int playerId)
        {
            _sessions.TryGetValue(playerId, out ClientSession? session);
            return session;
        }

        // ═══════════════════════════════════════
        // [8주차] 계정 중복 접속 막기
        //
        //   DB id → 접속 번호
        //   TryAdd 는 "없으면 넣기" 를 한 번에 합니다.
        //   두 명이 동시에 같은 계정으로 들어와도 한 명만 성공합니다.
        // ═══════════════════════════════════════
        private readonly ConcurrentDictionary<int, int> _accounts = new();

        public bool TryClaimAccount(int dbPlayerId, int playerId)
            => _accounts.TryAdd(dbPlayerId, playerId);

        public void ReleaseAccount(int dbPlayerId, int playerId)
            => _accounts.TryRemove(new System.Collections.Generic.KeyValuePair<int, int>(dbPlayerId, playerId));

        /// <summary>현재 접속 중인 세션 전체</summary>
        public System.Collections.Generic.List<ClientSession> GetSessions()
        {
            return new System.Collections.Generic.List<ClientSession>(_sessions.Values);
        }

        // ═══════════════════════════════════════
        // 브로드캐스트 — 모든 접속자에게 전송
        // ═══════════════════════════════════════
        public void Broadcast(object packet, int exceptPlayerId = -1)
        {
            foreach (var pair in _sessions)
            {
                if (pair.Key == exceptPlayerId) continue;
                _ = pair.Value.SendAsync(packet);
            }
        }

        // ═══════════════════════════════════════
        // 시작 화면
        // ═══════════════════════════════════════
        private void PrintBanner()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════╗");
            Console.WriteLine("║      배틀아레나 게임 서버            ║");
            Console.WriteLine("║      동양게임즈 서버개발팀           ║");
            Console.WriteLine("╠════════════════════════════════════╣");
            Console.WriteLine("║  TICKET-041  판 결과가 DB 에 남는다  ║");
            Console.WriteLine("║  TCP 7777 (로그인) + UDP 7778 (조작) ║");
            Console.WriteLine("╚════════════════════════════════════╝");
            Console.ResetColor();

            Console.WriteLine();
        }

        // ═══════════════════════════════════════
        // 진입점
        // ═══════════════════════════════════════
        public static async Task Main(string[] args)
        {
            // ── 실행 옵션 ──
            //   --round 15    한 판을 15초로   (저장을 빨리 확인하고 싶을 때)
            //   Visual Studio : 프로젝트 속성 → 디버그 → 명령줄 인수
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];

                if (a == "--round" && i + 1 < args.Length && int.TryParse(args[i + 1], out int sec) && sec >= 5)
                {
                    GameLoop.RoundSeconds = sec;
                    i++;
                }
            }

            GameServer server = new GameServer();

            try
            {
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[서버 에러] {ex.Message}");
                Console.WriteLine("아무 키나 누르면 종료합니다.");
                Console.ReadKey();
            }
        }
    }
}
