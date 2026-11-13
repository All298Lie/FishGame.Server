using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BattleArenaServer
{
    /// <summary>맵에 떨어져 있는 아이템 하나</summary>
    public class Item
    {
        public int Id;
        public int X;
        public int Y;

        /// <summary>
        /// 8주차 추가 : 아이템 종류 — 클라이언트의 enum ItemType 과 같은 번호
        ///
        ///   0  코인  1점
        ///   1  보석  5점
        /// </summary>
        public int Type;

        public int Points => Type == 1 ? 5 : 1;
    }

    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  [6주차 — TICKET-021] 게임 루프
    ///
    ///  ── 5주차까지 서버가 한 일 ──
    ///      받은 좌표를 그대로 적고, 그대로 뿌림        (우체국)
    ///
    ///  ── 오늘부터 서버가 하는 일 ──
    ///      방향만 받아서 좌표를 직접 계산하고,
    ///      아이템을 뿌리고, 누가 먹었는지 판정함        (심판)
    ///
    ///  ── 그런데 "방향만 받는다" 로는 부족합니다 ──
    ///
    ///      MOVE|3|152|20|0     DX 가 20      → 거리 검사로 막힙니다
    ///
    ///      MOVE|3|152|1|0      DX 가 1
    ///      MOVE|3|153|1|0        "
    ///      MOVE|3|154|1|0      1초에 300번   → 검사를 전부 통과합니다
    ///
    ///    두 번째는 규칙을 하나도 안 어겼습니다.
    ///    거리가 아니라 "횟수" 로 속도를 올린 것이라
    ///    검사를 아무리 늘려도 막히지 않습니다.
    ///
    ///        좌표를 보냄    서버가 위치를 안 가짐   →  순간이동
    ///        명령을 보냄    서버가 시계를 안 가짐   →  속도핵
    ///        틱이 꺼내 씀   서버가 둘 다 가짐       →  막힘   ← 오늘 만드는 것
    ///
    ///  ── 틱 하나에서 일어나는 일 ──
    ///
    ///      1. 받기     쌓인 MOVE 를 검사해서 사람별 줄에 세움
    ///      2. 걸음     각 줄에서 "딱 하나" 꺼내 좌표를 계산       ★
    ///      3. 게임     아이템 스폰 · 획득 판정 · 점수
    ///      4. 알리기   STATE · ITEMS 를 전체에 전송
    ///
    ///    핵심은 2번입니다. 몇 개가 쌓여 있든 한 번에 하나만 씁니다.
    ///    패킷을 1000개 보내도 속도는 그대로입니다.
    ///
    ///  ★ 이 루프는 한 번에 하나만 돕니다.
    ///    그래서 lock 이 하나도 없습니다.
    ///    "동시에 두 명이 먹으면?" 이라는 문제가 애초에 안 생깁니다.
    ///
    ///  ── 덤으로 생기는 것 ──
    ///
    ///    꺼낸 입력의 번호를 AckSeq 에 적어 STATE 로 돌려줍니다.
    ///    "네 152번까지 반영했고 그 결과가 (10,7)이다" 라는 뜻입니다.
    ///    클라이언트는 이 번호로 자기 예측을 맞춰볼 수 있습니다.
    ///    → 예제 H · 오늘 후반부
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class GameLoop
    {
        // ── 맵 ──
        public const int MAP_W = 20;
        public const int MAP_H = 12;

        /// <summary>틱 간격 (33ms ≒ 초당 30번)</summary>
        private const int TICK_MS = 33;

        /// <summary>몇 틱마다 아이템을 하나 뿌릴지 (60틱 ≒ 2초)</summary>
        private const int SPAWN_TICKS = 60;

        /// <summary>맵에 동시에 존재할 수 있는 아이템 수</summary>
        private const int MAX_ITEMS = 8;

        /// <summary>아이템을 주울 수 있는 거리 (0이면 같은 칸만)</summary>
        private const int PICK_RANGE = 1;

        /// <summary>보석이 나올 확률 (%) — 나머지는 코인</summary>
        private const int GEM_PERCENT = 20;

        // ═══════════════════════════════════════
        // 8주차 추가 : 판(라운드)
        //
        //   7주차까지 60초 타이머는 클라이언트에 있었습니다.
        //   클라이언트가 "끝났다" 고 하고, 자기 점수를 서버에 보내는 구조였죠.
        //   (GameManager.EndGame 의 SendGameResult 주석)
        //
        //   그러면 점수를 조작해서 보낼 수 있습니다.
        //   6주차에 위치를 서버로 가져왔듯이, 판도 서버로 가져옵니다.
        //
        //     판 시작   서버가 정함 → ROUND_START 로 알림
        //     점수      서버가 셈   → SCORE 로 알림
        //     판 끝     서버가 정함 → ROUND_END 로 알림 + DB 저장
        // ═══════════════════════════════════════

        /// <summary>한 판 길이 (초) — --round 10 처럼 바꿀 수 있습니다 (시연용)</summary>
        public static int RoundSeconds = 60;

        /// <summary>판 사이 쉬는 시간 (초) — 결과 화면을 보여주는 시간</summary>
        public const int BREAK_SECONDS = 5;

        /// <summary>
        /// 한 사람 줄에 쌓아둘 수 있는 입력 개수 (오늘의 핵심 상수 ①)
        ///
        /// ★ 이 상수는 속도핵을 막는 장치가 아닙니다.
        ///   속도는 STEP_TICKS(걸음 쿨타임)가 막습니다.
        ///   줄에 300개가 쌓여 있어도 132ms 에 한 칸 이상은 못 갑니다.
        ///
        /// 이 상수가 막는 것은 "밀린 입력이 한참 동안 재생되는 것" 입니다.
        ///
        ///   상한이 없으면
        ///     · 치터가 0.04초에 300개를 보내고 손을 떼도
        ///       캐릭터가 300 x 132ms = 약 40초 동안 혼자 걸어갑니다
        ///     · 그 사이 새로 누른 키는 300개 뒤에 줄을 서서
        ///       40초 뒤에야 반영됩니다
        ///     · 네트워크가 3초 끊겼던 정상 사용자도
        ///       3초 전 입력을 다 재생하느라 조작이 3초 밀립니다
        ///     · 줄이 끝없이 길어져 서버 메모리를 먹습니다
        ///
        ///   상한이 5면 밀려 봐야 0.66초 분량이고,
        ///   옛것부터 버리니 금방 최신 입력으로 돌아옵니다.
        ///
        /// 왜 0 이 아니라 5 인가 — 네트워크는 고르지 않습니다.
        /// 132ms 간격으로 보내도 가끔 두 개가 붙어서 옵니다.
        /// 조금 쌓아둬야 그 울렁임을 흡수해서 안 끊깁니다.
        /// </summary>
        private const int MAX_PENDING = 5;

        /// <summary>
        /// 한 걸음에 필요한 틱 수 (오늘의 핵심 상수 ②)
        ///
        /// 틱당 하나씩 꺼내 쓰기만 하면 최고 속도가 초당 30칸이 됩니다.
        /// 클라이언트는 0.132초마다 한 번 보내니 초당 7~8칸인데,
        /// 치터는 초당 30칸까지 낼 수 있다는 뜻입니다. 여전히 4배 빠릅니다.
        ///
        /// 그래서 걸음에도 쿨타임을 둡니다.
        /// 4틱 = 132ms = 클라이언트의 이동 쿨다운과 같은 값입니다.
        ///
        /// ★ 이 값과 GameConfig.MOVE_COOLDOWN 은 같아야 합니다.
        ///   서버가 더 느리면 입력이 쌓여서 조작이 밀리고,
        ///   서버가 더 빠르면 치터에게 여유를 주는 셈입니다.
        /// </summary>
        private const int STEP_TICKS = 4;

        private readonly UdpServer _udp;
        private readonly Random _rng = new Random();

        // 틱 루프 안에서만 건드리므로 일반 Dictionary 로 충분합니다
        private readonly Dictionary<int, Item> _items = new Dictionary<int, Item>();
        private int _nextItemId;

        /// <summary>
        /// 사람별 입력 줄
        ///
        /// UdpServer 의 큐는 전원이 섞인 한 줄입니다.
        /// 그러면 "이 사람이 몇 개 썼는지" 를 셀 수가 없습니다.
        /// 사람별로 나눠야 한 명당 하나씩 꺼내 쓸 수 있습니다.
        /// </summary>
        private readonly Dictionary<int, Queue<InputCommand>> _pending
            = new Dictionary<int, Queue<InputCommand>>();

        // ── 판 상태 ──
        private bool     _inRound;
        private int      _roundNo;
        private DateTime _roundStartedAt;
        private DateTime _phaseEndsAt;        // 판 끝 시각 또는 쉬는 시간 끝 시각

        public int  RoundNo => _roundNo;
        public bool InRound => _inRound;

        private readonly SaveQueue _saveQueue;

        private long _tick;
        private long _inputCount;
        private long _rejectCount;
        private long _dropCount;

        public long Tick        => _tick;
        public long InputCount  => _inputCount;
        public long RejectCount => _rejectCount;
        public long DropCount   => _dropCount;
        public int  ItemCount   => _items.Count;

        public GameLoop(UdpServer udp, SaveQueue saveQueue)
        {
            _udp       = udp;
            _saveQueue = saveQueue;
        }

        // ═══════════════════════════════════════
        // 루프
        // ═══════════════════════════════════════
        public async Task RunAsync()
        {
            Console.WriteLine($"[게임] 틱 루프 시작 ({1000 / TICK_MS}fps · 맵 {MAP_W}x{MAP_H})");
            Console.WriteLine($"[게임] 입력 버퍼 {MAX_PENDING}개 · " +
                              $"걸음 쿨타임 {STEP_TICKS}틱({STEP_TICKS * TICK_MS}ms)");
            Console.WriteLine($"[게임] 한 판 {RoundSeconds}초 · 쉬는 시간 {BREAK_SECONDS}초");
            Console.WriteLine();

            StartRound(GameServer.Instance.GetSessions());

            while (true)
            {
                await Task.Delay(TICK_MS);
                _tick++;

                var sessions = GameServer.Instance.GetSessions();

                // 0. 판 진행 (8주차)
                UpdateRound(sessions);

                // 1. 받은 입력을 검사해서 사람별 줄에 세운다
                IntakeInputs();

                // 2. 각 줄에서 하나씩 꺼내 걸음을 옮긴다
                StepPlayers(sessions);

                // 3. 게임 로직 — 판이 진행 중일 때만
                if (_inRound)
                {
                    if (_tick % SPAWN_TICKS == 0) SpawnItem();
                    await CheckPickupsAsync(sessions);
                }

                // 4. 알리기
                await BroadcastAsync(sessions);
            }
        }

        // ═══════════════════════════════════════
        // 0. 판 진행 — 시간이 되면 끝내고, 쉬는 시간이 끝나면 새로 시작
        // ═══════════════════════════════════════
        private void UpdateRound(List<ClientSession> sessions)
        {
            if (DateTime.Now < _phaseEndsAt) return;

            if (_inRound) EndRound(sessions);
            else          StartRound(sessions);
        }

        private void StartRound(List<ClientSession> sessions)
        {
            _roundNo++;
            _inRound        = true;
            _roundStartedAt = DateTime.Now;
            _phaseEndsAt    = _roundStartedAt.AddSeconds(RoundSeconds);

            // 점수와 맵을 새로
            _items.Clear();
            foreach (var s in sessions)
            {
                s.Score = 0;
                s.Coins = 0;
                s.Gems  = 0;
            }

            Console.WriteLine($"[판] {_roundNo}판 시작 ({RoundSeconds}초)");

            GameServer.Instance.Broadcast(new RoundStartPacket
            {
                RoundNo = _roundNo,
                Seconds = RoundSeconds,
            });

            _ = BroadcastScoreAsync(sessions);
        }

        /// <summary>
        /// 판 끝 — 순위를 매겨 알리고, DB 저장을 "맡긴다"
        /// </summary>
        private void EndRound(List<ClientSession> sessions)
        {
            _inRound     = false;
            _phaseEndsAt = DateTime.Now.AddSeconds(BREAK_SECONDS);
            _items.Clear();

            // ── 순위 : 점수 높은 순, 같으면 먼저 접속한 순 ──
            //   UdpEndPoint 가 있는 사람 = 실제로 게임 화면에 들어온 사람
            var players = sessions.FindAll(s => s.UdpEndPoint != null);
            players.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score)
                                                      : a.PlayerId.CompareTo(b.PlayerId));

            var board = new StringBuilder();
            for (int i = 0; i < players.Count; i++)
            {
                if (board.Length > 0) board.Append('|');
                board.Append($"{i + 1},{players[i].Nickname},{players[i].Score}");
            }

            Console.WriteLine($"[판] {_roundNo}판 끝 — {players.Count}명 · " +
                              (players.Count > 0 ? $"1위 {players[0].Nickname} {players[0].Score}점" : "참가자 없음"));

            GameServer.Instance.Broadcast(new RoundEndPacket
            {
                RoundNo   = _roundNo,
                Board     = board.ToString(),
                NextInSec = BREAK_SECONDS,
            });

            // ═══════════════════════════════════════
            // TODO 7 : 판 결과를 저장 큐에 넣기
            //
            //   1. MatchRecord 를 만든다
            //        RoundNo, StartedAt = _roundStartedAt, EndedAt = DateTime.Now
            //   2. players 를 순서대로 돌면서 MatchEntry 를 추가한다
            //        ★ PlayerId 에는 s.DbPlayerId 를 넣습니다 (접속 번호 아님)
            //        ★ DbPlayerId 가 0 인 손님은 건너뜁니다
            //        Ranking 은 i + 1
            //   3. 저장할 사람이 한 명 이상이면 _saveQueue.Enqueue(record)
            //
            //   ★ await MatchRepository.SaveAsync(...) 를 여기서 부르면 안 됩니다
            //     이 함수는 틱 안에서 돕니다. 저장이 끝날 때까지 게임 전체가 멈춥니다.
            //     (수업 시연에서 직접 봤습니다)
            // ═══════════════════════════════════════
            // ── 여기에 작성 ──


        }

        /// <summary>
        /// 막 들어온 사람에게 지금 판 상황을 알려 줍니다
        ///
        /// 판 도중에 들어오면 남은 시간만큼만 플레이합니다.
        /// 쉬는 시간에 들어오면 다음 판 시작 신호를 기다립니다.
        /// </summary>
        public async Task SendRoundInfoAsync(ClientSession s)
        {
            if (!_inRound) return;

            int remain = (int)Math.Ceiling((_phaseEndsAt - DateTime.Now).TotalSeconds);
            if (remain < 1) return;

            await s.SendAsync(new RoundStartPacket
            {
                RoundNo = _roundNo,
                Seconds = remain,
            });
        }

        /// <summary>통계 출력용 — 지금 판의 남은 시간</summary>
        public int RemainSeconds =>
            Math.Max(0, (int)Math.Ceiling((_phaseEndsAt - DateTime.Now).TotalSeconds));

        // ═══════════════════════════════════════
        // 1. 받기 — 검사해서 줄에 세우기만 합니다
        //
        //    여기서는 "움직이지" 않습니다.
        //    움직이는 것은 StepPlayers 가 합니다.
        // ═══════════════════════════════════════
        private void IntakeInputs()
        {
            while (_udp.Inputs.TryDequeue(out InputCommand cmd))
            {
                _inputCount++;

                // ── 검사 1 : 로그인한 사람인가 ──
                ClientSession? s = GameServer.Instance.FindSession(cmd.PlayerId);
                if (s == null)
                {
                    _rejectCount++;
                    continue;
                }

                // ── 검사 2 : 정말 그 사람이 보낸 건가 ──
                //   패킷에 적힌 PlayerId 를 그대로 믿으면
                //   누구든 남의 ID 를 적어 보낼 수 있습니다.
                if (s.UdpEndPoint == null)
                {
                    s.UdpEndPoint = cmd.From;
                    Console.WriteLine($"[UDP] {s.Nickname} 주소 등록 : {cmd.From}");
                }
                else if (!Equals(s.UdpEndPoint, cmd.From))
                {
                    _rejectCount++;
                    continue;
                }

                // ── 검사 3 : 지난 번호인가 ──
                //   UDP 는 순서를 보장하지 않습니다.
                //   늦게 도착한 옛날 패킷은 버립니다.
                if (cmd.Seq <= s.LastSeq)
                {
                    _rejectCount++;
                    continue;
                }
                s.LastSeq = cmd.Seq;

                // ── 검사 4 : 한 걸음 거리인가 ──
                if (Math.Abs(cmd.Dx) > 1 || Math.Abs(cmd.Dy) > 1)
                {
                    _rejectCount++;
                    continue;
                }

                // ── 통과 : 줄에 세운다 ──
                if (!_pending.TryGetValue(cmd.PlayerId, out Queue<InputCommand>? q))
                {
                    q = new Queue<InputCommand>();
                    _pending[cmd.PlayerId] = q;
                }

                // ★ 줄이 꽉 찼으면 "가장 오래된 것" 을 버립니다
                //
                //   정상 클라이언트는 여기 걸리지 않습니다.
                //   0.132초마다 한 번 보내고 서버도 그 속도로 꺼내니
                //   줄 길이가 0~1 사이에서 움직입니다.
                //
                //   걸리는 건 치터, 또는 네트워크가 끊겼다 돌아와서
                //   밀린 입력이 한꺼번에 도착한 경우입니다.
                //
                //   ── 왜 새것이 아니라 옛것을 버리나 ──
                //
                //   새것을 버리면 서버는 옛 입력(1~5번)만 처리하고
                //   AckSeq 가 5 에서 멈춥니다. 클라는 버려진 6~12번을
                //   "아직 확인 안 됨" 으로 계속 들고 있어서
                //   서버 위치 + 7칸에 머뭅니다. 서버와 영영 어긋납니다.
                //
                //   옛것을 버리면 남는 건 8~12번이고, 첫 처리에서
                //   AckSeq 가 8 이 됩니다. 클라는 1~8번을 한꺼번에 지우고
                //   바로 서버 위치로 맞춰집니다.
                //
                //   입력은 "그 순간의 의도" 라서 최신 것이 더 중요합니다.
                //
                //   속도핵을 쓰면 여기서 폐기 숫자가 크게 오릅니다.
                //   (속도를 막는 건 StepPlayers 의 걸음 쿨타임이고,
                //    여기는 그 흔적이 숫자로 남는 곳입니다)
                if (q.Count >= MAX_PENDING)
                {
                    q.Dequeue();          // 가장 오래된 것을 버리고
                    _dropCount++;
                }

                q.Enqueue(cmd);           // 방금 온 것은 넣는다
            }
        }

        // ═══════════════════════════════════════
        // 2. 걸음 — 서버가 시계를 갖는 지점
        //
        //    조건 두 개를 모두 만족해야 한 걸음입니다.
        //
        //      (1) 걸음 쿨타임이 끝났나   STEP_TICKS 마다 한 번
        //      (2) 줄에 입력이 있나        없으면 가만히 있는다
        //
        //    줄에 5개가 쌓여 있어도 쿨타임이 안 끝났으면 안 움직입니다.
        //    남은 것은 다음 차례로 넘어갑니다.
        //
        //    → 패킷을 아무리 빨리 보내도 속도가 안 빨라집니다
        // ═══════════════════════════════════════
        private void StepPlayers(List<ClientSession> sessions)
        {
            foreach (var s in sessions)
            {
                // (1) 아직 걸음 차례가 아니면 넘어갑니다
                if (_tick < s.NextStepTick) continue;

                // (2) 줄에 입력이 없으면 가만히 있습니다
                if (!_pending.TryGetValue(s.PlayerId, out Queue<InputCommand>? q)) continue;
                if (q.Count == 0) continue;

                InputCommand cmd = q.Dequeue();

                s.NextStepTick = _tick + STEP_TICKS;

                // 좌표는 서버가 계산합니다
                s.X = Math.Clamp(s.X + cmd.Dx, 0, MAP_W - 1);
                s.Y = Math.Clamp(s.Y + cmd.Dy, 0, MAP_H - 1);

                // ★ 어디까지 반영했는지 적어둡니다
                //
                //   이 번호를 STATE 에 실어 보내면 클라이언트가
                //   "내 152번까지는 서버가 처리했구나" 를 알 수 있습니다.
                //   그래야 자기 예측을 맞춰볼 수 있습니다.
                s.AckSeq = cmd.Seq;
            }

            // 나간 사람의 줄 치우기
            if (_pending.Count > sessions.Count)
            {
                var live = new HashSet<int>();
                foreach (var s in sessions) live.Add(s.PlayerId);

                var gone = new List<int>();
                foreach (int id in _pending.Keys)
                    if (!live.Contains(id)) gone.Add(id);

                foreach (int id in gone) _pending.Remove(id);
            }
        }

        // ═══════════════════════════════════════
        // 3. 아이템
        // ═══════════════════════════════════════
        private void SpawnItem()
        {
            if (_items.Count >= MAX_ITEMS) return;

            // 틱 루프 안에서만 증가시키므로 Interlocked 가 필요 없습니다
            int id = ++_nextItemId;

            _items[id] = new Item
            {
                Id   = id,
                X    = _rng.Next(0, MAP_W),
                Y    = _rng.Next(0, MAP_H),
                Type = _rng.Next(100) < GEM_PERCENT ? 1 : 0,
            };
        }

        private async Task CheckPickupsAsync(List<ClientSession> sessions)
        {
            if (_items.Count == 0) return;

            var taken = new List<(ClientSession who, Item what)>();

            foreach (var s in sessions)
            {
                if (s.UdpEndPoint == null) continue;   // 아직 게임에 안 들어온 사람

                foreach (var item in _items.Values)
                {
                    if (Math.Abs(item.X - s.X) > PICK_RANGE) continue;
                    if (Math.Abs(item.Y - s.Y) > PICK_RANGE) continue;

                    taken.Add((s, item));
                    break;      // 한 틱에 하나만
                }
            }

            // ★ 먼저 찾은 사람이 가져갑니다.
            //   틱 루프가 혼자 돌기 때문에 순서가 확정됩니다.
            //   두 명이 "동시에" 먹는 일은 일어나지 않습니다.
            foreach (var (who, what) in taken)
            {
                if (!_items.Remove(what.Id)) continue;   // 이미 누가 가져감

                who.Score += what.Points;
                if (what.Type == 1) who.Gems++;
                else                who.Coins++;

                GameServer.Instance.Broadcast(new TakenPacket
                {
                    ItemId   = what.Id,
                    PlayerId = who.PlayerId,
                    Nickname = who.Nickname,
                    ItemType = what.Type,
                    Points   = what.Points,
                });
            }

            if (taken.Count > 0)
                await BroadcastScoreAsync(sessions);
        }

        private Task BroadcastScoreAsync(List<ClientSession> sessions)
        {
            var sb = new StringBuilder();

            foreach (var s in sessions)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append($"{s.PlayerId},{s.Nickname},{s.Score}");
            }

            GameServer.Instance.Broadcast(new ScorePacket { Board = sb.ToString() });
            return Task.CompletedTask;
        }

        // ═══════════════════════════════════════
        // 4. 알리기
        //
        //    ★ STATE 맨 앞에 AckSeq 가 붙습니다
        //
        //      STATE|152|id,닉,x,y|id,닉,x,y
        //            ↑
        //      받는 사람마다 다른 값
        //
        //    사람 목록은 모두에게 같지만 AckSeq 는 각자 다릅니다.
        //    그래서 본문은 한 번만 만들고 앞머리만 바꿔 붙입니다.
        // ═══════════════════════════════════════
        private async Task BroadcastAsync(List<ClientSession> sessions)
        {
            if (sessions.Count == 0) return;

            // 본문 : |id,닉네임,x,y|id,닉네임,x,y...
            var body = new StringBuilder();
            bool any = false;

            foreach (var s in sessions)
            {
                if (s.UdpEndPoint == null) continue;
                body.Append($"|{s.PlayerId},{s.Nickname},{s.X},{s.Y}");
                any = true;
            }

            if (!any) return;

            // ITEMS|아이템id,x,y,종류|...      (8주차 : 종류 추가 — 0 코인, 1 보석)
            var items = new StringBuilder(UdpPacket.ITEMS);
            foreach (var it in _items.Values)
                items.Append($"|{it.Id},{it.X},{it.Y},{it.Type}");

            string bodyMsg = body.ToString();
            string itemsMsg = items.ToString();

            foreach (var s in sessions)
            {
                if (s.UdpEndPoint == null) continue;

                string stateMsg = $"{UdpPacket.STATE}|{s.AckSeq}{bodyMsg}";

                await _udp.SendToAsync(s.UdpEndPoint, stateMsg);
                await _udp.SendToAsync(s.UdpEndPoint, itemsMsg);
            }
        }
    }
}
