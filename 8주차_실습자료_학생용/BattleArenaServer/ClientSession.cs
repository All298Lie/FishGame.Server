using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BattleArenaServer
{
    /// <summary>
    /// 클라이언트 한 명과의 연결을 담당
    ///
    /// 클라이언트가 접속할 때마다 이 객체가 하나씩 만들어집니다.
    /// 100명이 접속하면 ClientSession 객체도 100개가 됩니다.
    /// </summary>
    public class ClientSession
    {
        public int    PlayerId { get; private set; }
        public string Nickname { get; private set; } = "Unknown";

        // ── 5주차 추가 : 위치 정보 ──
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// 이 플레이어의 UDP 주소
        ///
        /// TCP 소켓과 UDP 주소는 별개입니다.
        /// 클라이언트가 UDP로 첫 MOVE 패킷을 보내면 그때 채워집니다.
        /// </summary>
        public System.Net.EndPoint? UdpEndPoint { get; set; }

        /// <summary>
        /// 마지막으로 받은 시퀀스 번호
        ///
        /// UDP는 순서를 보장하지 않아서
        /// 이것보다 작은 번호가 오면 오래된 패킷이므로 버립니다.
        /// </summary>
        public int LastSeq { get; set; }

        /// <summary>
        /// 마지막으로 "실제로 반영한" 시퀀스 번호 (6주차 추가)
        ///
        /// LastSeq 와 헷갈리기 쉬운데 역할이 다릅니다.
        ///
        ///   LastSeq   받아본 중 가장 큰 번호   — 중복·역순 패킷을 걸러내는 용도
        ///   AckSeq    걸음에 써먹은 번호       — 클라이언트에게 돌려주는 영수증
        ///
        /// 입력은 줄에 쌓여 있다가 틱마다 하나씩 쓰이므로
        /// 보통 AckSeq 가 LastSeq 보다 몇 개 뒤처져 있습니다.
        ///
        /// 이 값을 STATE 로 보내주면 클라이언트가
        /// "서버는 내 152번까지 처리했다" 를 알고
        /// 그 이후 입력만 다시 적용해 자기 예측을 맞춥니다.
        /// </summary>
        public int AckSeq { get; set; }

        /// <summary>
        /// 다음 걸음이 가능해지는 틱 (6주차 추가)
        ///
        /// 걸음을 옮길 때마다 "지금 틱 + STEP_TICKS" 로 밀어둡니다.
        /// 그 틱이 되기 전에는 입력이 쌓여 있어도 움직이지 않습니다.
        ///
        /// 이 한 줄이 속도핵을 막습니다.
        /// 클라이언트가 초당 300번 보내도 걸음 수는 서버가 셉니다.
        /// </summary>
        public long NextStepTick { get; set; }

        /// <summary>
        /// 모은 아이템 개수 (6주차 추가)
        ///
        /// 이 값도 서버만 바꿉니다.
        /// 클라이언트는 결과를 받아서 보여주기만 합니다.
        /// </summary>
        public int Score { get; set; }

        // ── 8주차 추가 ──

        /// <summary>
        /// DB 의 players.id (로그인할 때 채워집니다)
        ///
        /// ★ PlayerId 와 헷갈리지 마세요
        ///
        ///   PlayerId     접속할 때마다 새로 받는 번호   1, 2, 3 ...   (서버를 끄면 사라짐)
        ///   DbPlayerId   DB 에 영원히 남는 번호         홍길동 = 1    (언제 들어와도 같음)
        ///
        /// 판 결과를 저장할 때는 DbPlayerId 를 씁니다.
        /// 0 이면 DB 없이 들어온 손님입니다. 결과가 저장되지 않습니다.
        /// </summary>
        public int DbPlayerId { get; set; }

        /// <summary>DB 에 저장된 최고 점수 (로그인할 때 읽어 옴)</summary>
        public int BestScore { get; set; }

        /// <summary>이번 판에 먹은 코인 · 보석 개수</summary>
        public int Coins { get; set; }
        public int Gems  { get; set; }

        /// <summary>닉네임 최대 길이 — players.nickname VARCHAR(20) 과 같아야 합니다</summary>
        public const int MAX_NICKNAME = 20;

        private readonly Socket _socket;
        private bool _isConnected = true;

        public ClientSession(Socket socket, int playerId)
        {
            _socket   = socket;
            PlayerId  = playerId;
        }

        // ═══════════════════════════════════════
        // 수신 루프 — 클라이언트가 보낸 패킷을 계속 받음
        // ═══════════════════════════════════════
        public async Task RunAsync()
        {
            Console.WriteLine($"[접속] ID:{PlayerId}  {_socket.RemoteEndPoint}");

            try
            {
                while (_isConnected)
                {
                    // 패킷 하나 받기
                    string? json = await PacketHelper.ReceiveAsync(_socket);

                    // null이면 연결이 끊긴 것
                    if (json == null) break;

                    Console.WriteLine($"[수신] ID:{PlayerId}  {json}");

                    // 패킷 처리
                    await HandlePacketAsync(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[에러] ID:{PlayerId}  {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        // ═══════════════════════════════════════
        // 패킷 처리
        // ═══════════════════════════════════════
        private async Task HandlePacketAsync(string json)
        {
            // JSON에서 Type만 먼저 꺼내기
            dynamic? packet = JsonConvert.DeserializeObject(json);
            if (packet == null) return;

            int type = (int)packet.Type;

            switch (type)
            {
                case PacketType.LOGIN:
                    await HandleLoginAsync(json);
                    break;

                default:
                    Console.WriteLine($"[경고] 모르는 패킷 타입: {type}");
                    break;
            }
        }

        /// <summary>
        /// 로그인 처리
        ///
        /// ── 8주차 : DB 에서 플레이어를 찾거나 만듭니다 ──
        ///
        ///   여기서는 await 로 DB 를 기다려도 됩니다.
        ///   이 함수는 "이 사람 전용" 수신 루프에서 돌기 때문에
        ///   기다리는 동안 멈추는 건 이 사람의 로그인뿐입니다.
        ///   게임 루프(GameLoop)는 계속 돕니다.
        /// </summary>
        private async Task HandleLoginAsync(string json)
        {
            LoginPacket? login = JsonConvert.DeserializeObject<LoginPacket>(json);
            if (login == null) return;

            // ── 1. 닉네임 검사 — DB 에 보내기 전에 ──
            //   VARCHAR(20) 보다 길면 MySQL 이 1406 에러로 거부합니다.
            //   DB 까지 가기 전에 서버가 먼저 막습니다.
            string nick = (login.Nickname ?? "").Trim();
            if (nick.Length == 0)            nick = $"Player{PlayerId}";
            if (nick.Length > MAX_NICKNAME)  nick = nick.Substring(0, MAX_NICKNAME);

            Nickname = nick;
            string greeting;

            // ── 2. DB 에서 찾거나 만들기 ──
            if (Db.IsAvailable)
            {
                try
                {
                    PlayerRecord player = await PlayerRepository.LoginAsync(nick);

                    // ── 같은 계정이 이미 접속해 있으면 막기 ──
                    //   한 판에 같은 플레이어가 두 줄이면 match_results 의 기본키가 겹쳐
                    //   그 판 저장이 통째로 실패합니다.
                    //   "확인하고 등록" 을 한 번에(TryAdd) 해야 두 명이 동시에 들어와도 안 겹칩니다.
                    if (!GameServer.Instance.TryClaimAccount(player.Id, PlayerId))
                    {
                        Console.WriteLine($"[로그인] 거부 — {player.Nickname} 은(는) 이미 접속 중");
                        await SendAsync(new NoticePacket
                        {
                            Type    = PacketType.PLAYER_OUT,
                            Message = $"{player.Nickname} 계정은 이미 접속 중입니다."
                        });
                        Disconnect();
                        return;
                    }

                    DbPlayerId = player.Id;
                    Nickname   = player.Nickname;     // ★ DB 에 있는 이름이 진짜입니다
                    BestScore  = player.BestScore;

                    greeting = player.GamesPlayed == 0
                        ? $"{Nickname}님, 처음 오셨네요! 환영합니다."
                        : $"{Nickname}님, 다시 오셨네요! 최고 점수 {player.BestScore}점 · {player.GamesPlayed}판";
                }
                catch (NotImplementedException ex)
                {
                    // 아직 TODO 를 안 채운 경우
                    Console.WriteLine($"[DB] {ex.Message} 가 비어 있습니다 — 손님으로 입장");
                    DbPlayerId = 0;
                    greeting   = $"{Nickname}님, 환영합니다. (서버 로그인 코드 작성 중 — 저장되지 않습니다)";
                }
                catch (Exception ex)
                {
                    // DB 가 죽어 있어도 게임은 됩니다. 저장만 안 됩니다.
                    Console.WriteLine($"[DB] 로그인 실패 — 손님으로 입장 : {ex.Message}");
                    DbPlayerId = 0;
                    greeting   = $"{Nickname}님, 환영합니다. (DB 연결 실패 — 이번 판은 저장되지 않습니다)";
                }
            }
            else
            {
                greeting = $"{Nickname}님, 환영합니다. (DB 없이 실행 중 — 저장되지 않습니다)";
            }

            Console.WriteLine($"[로그인] {Nickname} (접속번호 {PlayerId} · DB id {DbPlayerId})");

            // 1. 본인에게 환영 메시지
            await SendAsync(new WelcomePacket
            {
                PlayerId  = PlayerId,
                Message   = greeting,
                Nickname  = Nickname,
                BestScore = BestScore,
            });

            // 판이 진행 중이면 남은 시간을 알려 줍니다
            await GameServer.Instance.Loop.SendRoundInfoAsync(this);

            // 2. 다른 사람들에게 입장 알림
            GameServer.Instance.Broadcast(new NoticePacket
            {
                Type    = PacketType.PLAYER_IN,
                Message = $"{Nickname}님이 입장했습니다."
            }, exceptPlayerId: PlayerId);
        }

        // ═══════════════════════════════════════
        // 전송
        // ═══════════════════════════════════════
        public async Task SendAsync(object packet)
        {
            if (!_isConnected) return;

            try
            {
                await PacketHelper.SendAsync(_socket, packet);
            }
            catch
            {
                Disconnect();
            }
        }

        // ═══════════════════════════════════════
        // 연결 종료
        // ═══════════════════════════════════════
        public void Disconnect()
        {
            if (!_isConnected) return;
            _isConnected = false;

            Console.WriteLine($"[퇴장] {Nickname} (ID:{PlayerId})");

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            catch { }

            // 서버에서 제거
            GameServer.Instance.RemoveSession(PlayerId);
            if (DbPlayerId != 0)
                GameServer.Instance.ReleaseAccount(DbPlayerId, PlayerId);

            // 다른 사람들에게 퇴장 알림
            GameServer.Instance.Broadcast(new NoticePacket
            {
                Type    = PacketType.PLAYER_OUT,
                Message = $"{Nickname}님이 나갔습니다."
            });
        }
    }
}
