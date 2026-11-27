using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySqlConnector;
using StackExchange.Redis;

namespace RedisLab
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════
    ///  예제 R — Redis 명령을 C# 으로 옮기기
    ///
    ///  8주차 예제 I(SqlLab)와 같은 연습장입니다.
    ///  완성본이니 고치지 말고, 메뉴를 하나씩 실행하면서 아래 코드를 읽으세요.
    ///
    ///  ── MySQL 과 다른 점 ──
    ///
    ///      MySQL                                   Redis
    ///      ─────────────────────────────────────   ─────────────────────────────────────
    ///      SQL 문장 하나를 보냄                     명령 하나를 보냄 (ZADD · GET ...)
    ///      new MySqlCommand(sql, conn)             db.SortedSetAddAsync(...)  ← 명령마다 메서드
    ///      함수마다 연결 열고 닫기 (풀)             연결 하나(ConnectionMultiplexer)를 끝까지 공유
    ///      결과는 표 → reader 로 한 줄씩            결과는 값 · 배열 → 바로 C# 값
    ///      없으면 0줄 / NULL                       없으면 nil → C# 에서 null (IsNull)
    ///
    ///  ── 먼저 할 것 ──
    ///      Redis 서버를 켜 둡니다. 기본 포트 6379. (MSOpenTech Redis 는 설치하면 서비스로 켜져 있습니다)
    ///      이 연습장은 lab: 으로 시작하는 키만 씁니다. 게임 서버 키(ba:)는 건드리지 않습니다.
    /// ═══════════════════════════════════════════════════════════
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Redis 주소 — "주소:포트"
        ///
        ///   abortConnect=false  처음에 못 붙어도 예외로 죽지 않고 계속 다시 시도
        ///   connectTimeout      ms 단위 (8주차 Connection Timeout=3 과 같은 역할)
        /// </summary>
        private const string REDIS = "127.0.0.1:6379,connectTimeout=3000,abortConnect=false";

        /// <summary>8번 속도 비교에서만 씁니다 (8주차 SqlLab 과 같은 계정)</summary>
        private const string MYSQL =
            "Server=127.0.0.1;Port=3306;Database=battlearena_game;" +
            "User ID=ba_server;Password=ba-server-2026;";

        // ★ 연결은 하나 — 프로그램이 끝날 때까지 같이 씁니다
        private static ConnectionMultiplexer _mux = null!;
        private static IDatabase _db = null!;

        public static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding  = Encoding.UTF8;

            // ── 1. 연결 (프로그램 시작 때 한 번) ──
            _mux = await ConnectionMultiplexer.ConnectAsync(REDIS);
            _db  = _mux.GetDatabase();

            if (!_mux.IsConnected)
            {
                Console.WriteLine("  Redis 에 연결하지 못했습니다. 127.0.0.1:6379 에 Redis 가 켜져 있는지 확인하세요. (Windows : 서비스 Redis)");
                Console.WriteLine("  (켜면 자동으로 다시 붙습니다. 메뉴는 그대로 쓸 수 있습니다.)");
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════════════╗");
                Console.WriteLine("║   예제 R — Redis 명령을 C# 으로 옮기기          ║");
                Console.WriteLine("╚══════════════════════════════════════════════╝");
                Console.WriteLine("  1. 연결 확인                 PING");
                Console.WriteLine("  2. 문자열 · 만료              SET · GET · EXPIRE · TTL");
                Console.WriteLine("  3. 숫자 세기 (동시에 100번)   GET+SET vs INCR");
                Console.WriteLine("  4. 묶음 (해시)                HMSET · HGETALL · HINCRBY");
                Console.WriteLine("  5. ★ 랭킹 (정렬된 집합)       ZADD · ZREVRANGE · ZREVRANK");
                Console.WriteLine("  6. 최고 점수만 남기기         ZSCORE → 비교 → ZADD");
                Console.WriteLine("  7. 없는 값은 null             nil → IsNull · null");
                Console.WriteLine("  8. 속도 비교 (10만 명)        MySQL vs Redis  (02_bench_seed.sql 먼저)");
                Console.WriteLine("  0. 종료");
                Console.Write("  > ");

                string? key = Console.ReadLine()?.Trim();
                Console.WriteLine();

                try
                {
                    switch (key)
                    {
                        case "1": await Step1_PingAsync();      break;
                        case "2": await Step2_StringAsync();    break;
                        case "3": await Step3_CounterAsync();   break;
                        case "4": await Step4_HashAsync();      break;
                        case "5": await Step5_SortedSetAsync(); break;
                        case "6": await Step6_BestOnlyAsync(); break;
                        case "7": await Step7_NullAsync();      break;
                        case "8": await Step8_SpeedAsync();     break;
                        case "0": case null: return;
                        default:  Console.WriteLine("  번호를 다시 고르세요."); break;
                    }
                }
                catch (RedisConnectionException ex)
                {
                    // 메시지가 아주 길어서 앞부분만 보여 줍니다 (" - " 뒤는 진단 정보)
                    Console.WriteLine($"  [Redis 연결 에러] {ex.Message.Split(" - ")[0]}");
                    Console.WriteLine("  Redis 가 켜져 있는지 확인하세요. (Windows : 서비스 Redis)");
                }
                catch (RedisTimeoutException ex)
                {
                    Console.WriteLine($"  [Redis 시간 초과] {ex.Message.Split(", ")[0]}");
                }
                catch (RedisServerException ex)
                {
                    // Redis 가 돌려준 에러 — 명령 이름이나 타입이 틀린 경우 (WRONGTYPE 등)
                    Console.WriteLine($"  [Redis 에러] {ex.Message}");
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"  [MySQL 에러 {ex.Number}] {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 1. 연결 확인
        //
        //   redis-cli                 C#
        //   ───────────────────────   ───────────────────────────────
        //   (redis-cli 실행)          ConnectionMultiplexer.ConnectAsync(주소)  ← Main 에서 한 번
        //   PING → PONG               db.PingAsync() → 왕복 시간(TimeSpan)
        // ═══════════════════════════════════════════════════════════
        private static async Task Step1_PingAsync()
        {
            Cmd("PING");

            TimeSpan t = await _db.PingAsync();

            Console.WriteLine($"  연결됨 : {_mux.IsConnected}");
            Console.WriteLine($"  왕복   : {t.TotalMilliseconds:0.000} ms");
            Console.WriteLine($"  서버   : {string.Join(", ", _mux.GetEndPoints().Select(e => e.ToString()))}");
            Console.WriteLine();
            Console.WriteLine("  ★ 8주차 MySQL 은 함수마다 연결을 열고 닫았습니다 (커넥션 풀).");
            Console.WriteLine("    Redis 는 _mux 하나를 프로그램 끝까지 같이 씁니다.");
            Console.WriteLine("    여러 쓰레드의 명령을 한 줄로 세워 보내고, 답을 순서대로 짝지어 줍니다.");
        }

        // ═══════════════════════════════════════════════════════════
        // 2. 문자열 · 만료
        //
        //   SET 키 값             db.StringSetAsync(키, 값)
        //   GET 키                db.StringGetAsync(키)          → RedisValue
        //   SET 키 값 EX 5        db.StringSetAsync(키, 값, TimeSpan.FromSeconds(5))
        //   TTL 키                db.KeyTimeToLiveAsync(키)      → TimeSpan? (만료 없으면 null)
        //
        //   ★ 만료(TTL) : 시간이 지나면 Redis 가 알아서 지웁니다.
        //     "5분짜리 로그인 토큰", "이번 주 랭킹" 처럼 기한이 있는 데이터에 씁니다.
        //     MySQL 에는 이런 기능이 없어서 직접 지워야 합니다.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step2_StringAsync()
        {
            Cmd("SET lab:notice \"오늘 저녁 8시 점검\"");
            await _db.StringSetAsync("lab:notice", "오늘 저녁 8시 점검");

            Cmd("GET lab:notice");
            RedisValue v = await _db.StringGetAsync("lab:notice");
            Console.WriteLine($"  → \"{v}\"");

            Cmd("TTL lab:notice");
            Console.WriteLine($"  → {(await _db.KeyTimeToLiveAsync("lab:notice"))?.ToString() ?? "null (만료 없음 — 영원히 남음)"}");

            Cmd("SET lab:event \"보석 2배\" EX 3      (3초 뒤 자동 삭제)");
            await _db.StringSetAsync("lab:event", "보석 2배", TimeSpan.FromSeconds(3));

            for (int i = 0; i < 5; i++)
            {
                RedisValue e = await _db.StringGetAsync("lab:event");
                TimeSpan? ttl = await _db.KeyTimeToLiveAsync("lab:event");
                Console.WriteLine($"  {i}초 : GET → {(e.IsNull ? "(nil) 사라짐" : $"\"{e}\"")}   TTL → {(ttl == null ? "-" : $"{ttl.Value.TotalSeconds:0.0}초")}");
                await Task.Delay(1000);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 3. 숫자 세기 — 동시에 100번 더하기
        //
        //   ✗ GET 해서 C# 에서 +1 하고 SET       → 사이에 다른 요청이 끼어듦
        //   ✓ INCR 키                            → Redis 가 한 번에 (원자적)
        //
        //   8주차 SaveQueue 의 _pending++ 를 Interlocked.Increment 로 바꾼 이유와 같습니다.
        //   Redis 는 명령을 하나씩 차례로 처리하므로, 명령 하나 안에서는 끼어들 틈이 없습니다.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step3_CounterAsync()
        {
            const int N = 100;

            // ✗ 읽고 → 더하고 → 쓰기 (세 단계)
            await _db.StringSetAsync("lab:visits:bad", 0);
            Cmd($"(GET lab:visits:bad → +1 → SET) × {N}명 동시에");

            var bad = new List<Task>();
            for (int i = 0; i < N; i++)
            {
                bad.Add(Task.Run(async () =>
                {
                    long now = (long)await _db.StringGetAsync("lab:visits:bad");
                    await _db.StringSetAsync("lab:visits:bad", now + 1);
                }));
            }
            await Task.WhenAll(bad);
            Console.WriteLine($"  → {await _db.StringGetAsync("lab:visits:bad")}   (100 이어야 하는데...)");

            // ✓ INCR (한 단계)
            await _db.StringSetAsync("lab:visits:good", 0);
            Cmd($"INCR lab:visits:good × {N}명 동시에");

            var good = new List<Task>();
            for (int i = 0; i < N; i++)
                good.Add(Task.Run(() => _db.StringIncrementAsync("lab:visits:good")));
            await Task.WhenAll(good);
            Console.WriteLine($"  → {await _db.StringGetAsync("lab:visits:good")}");
        }

        // ═══════════════════════════════════════════════════════════
        // 4. 묶음 (해시) — 키 하나에 이름:값 여러 개
        //
        //   HMSET 키 칸 값 칸 값 ...    db.HashSetAsync(키, HashEntry[])
        //     (Redis 3.0 의 HSET 은 칸 하나만. 여러 칸은 HMSET — 라이브러리가 알아서 HMSET 을 보냄)
        //     (Redis 4.0 부터는 HSET 도 여러 칸을 받습니다)
        //   HGET 키 칸                  db.HashGetAsync(키, 칸)
        //   HGETALL 키                  db.HashGetAllAsync(키)       → HashEntry[]
        //   HINCRBY 키 칸 1             db.HashIncrementAsync(키, 칸, 1)
        //
        //   MySQL 의 "한 줄" 과 비슷하지만 테이블 · 칸 정의가 없습니다.
        //   아무 칸이나 그때그때 넣습니다. (스키마가 없다 → 10주차 MongoDB 와 연결)
        // ═══════════════════════════════════════════════════════════
        private static async Task Step4_HashAsync()
        {
            Cmd("HMSET lab:player:1 nickname 홍길동 best 23 games 2");
            await _db.HashSetAsync("lab:player:1", new HashEntry[]
            {
                new HashEntry("nickname", "홍길동"),
                new HashEntry("best",     23),
                new HashEntry("games",    2),
            });

            Cmd("HINCRBY lab:player:1 games 1");
            long games = await _db.HashIncrementAsync("lab:player:1", "games", 1);
            Console.WriteLine($"  → {games}");

            Cmd("HGET lab:player:1 nickname");
            Console.WriteLine($"  → \"{await _db.HashGetAsync("lab:player:1", "nickname")}\"");

            Cmd("HGETALL lab:player:1");
            foreach (HashEntry e in await _db.HashGetAllAsync("lab:player:1"))
                Console.WriteLine($"  {e.Name,-10} = {e.Value}");
        }

        // ═══════════════════════════════════════════════════════════
        // 5. ★ 랭킹 — 정렬된 집합 (Sorted Set)
        //
        //   ZADD 키 점수 멤버 ...            db.SortedSetAddAsync(키, SortedSetEntry[])
        //   ZREVRANGE 키 0 2 WITHSCORES      db.SortedSetRangeByRankWithScoresAsync(키, 0, 2, Order.Descending)
        //   ZREVRANK 키 멤버                 db.SortedSetRankAsync(키, 멤버, Order.Descending)   → long? (0부터!)
        //   ZSCORE 키 멤버                   db.SortedSetScoreAsync(키, 멤버)                   → double?
        //   ZCARD 키                         db.SortedSetLengthAsync(키)
        //
        //   ★ REV = 큰 점수부터. 랭킹은 거의 항상 REV 쪽을 씁니다.
        //   ★ 순위는 0 부터. 1등이 0 입니다. 화면에 보일 때 + 1.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step5_SortedSetAsync()
        {
            await _db.KeyDeleteAsync("lab:rank");

            Cmd("ZADD lab:rank 23 홍길동 12 김철수 21 이영희 30 최강자 1 새내기");
            await _db.SortedSetAddAsync("lab:rank", new SortedSetEntry[]
            {
                new SortedSetEntry("홍길동", 23),
                new SortedSetEntry("김철수", 12),
                new SortedSetEntry("이영희", 21),
                new SortedSetEntry("최강자", 30),
                new SortedSetEntry("새내기", 1),
            });

            Cmd("ZREVRANGE lab:rank 0 2 WITHSCORES      (TOP 3)");
            SortedSetEntry[] top = await _db.SortedSetRangeByRankWithScoresAsync("lab:rank", 0, 2, Order.Descending);
            for (int i = 0; i < top.Length; i++)
                Console.WriteLine($"  {i + 1}위  {top[i].Element,-6} {top[i].Score}점");

            Cmd("ZREVRANK lab:rank 홍길동");
            long? r = await _db.SortedSetRankAsync("lab:rank", "홍길동", Order.Descending);
            Console.WriteLine($"  → {r}   ← 0부터 셉니다. 화면에는 {r + 1}위");

            Cmd("ZSCORE lab:rank 홍길동");
            Console.WriteLine($"  → {await _db.SortedSetScoreAsync("lab:rank", "홍길동")}");

            Cmd("ZCARD lab:rank");
            Console.WriteLine($"  → {await _db.SortedSetLengthAsync("lab:rank")}명");

            Cmd("ZADD lab:rank 25 김철수      (김철수가 25점을 받음)");
            await _db.SortedSetAddAsync("lab:rank", "김철수", 25);
            Console.WriteLine($"  김철수 순위 → {await _db.SortedSetRankAsync("lab:rank", "김철수", Order.Descending) + 1}위   (넣는 순간 자리를 잡습니다)");
        }

        // ═══════════════════════════════════════════════════════════
        // 6. 최고 점수만 남기기
        //
        //   ✗ 그냥 ZADD 하면 덮어씁니다 — 23점이던 사람이 이번 판 15점이면 15점
        //   ✓ 읽고 → 비교하고 → 클 때만 쓰기
        //       ZSCORE 키 멤버          db.SortedSetScoreAsync(키, 멤버)    → double? (없으면 null)
        //       (더 크면) ZADD          db.SortedSetAddAsync(키, 멤버, 점수)
        //
        //   ★ 세 단계라 3번처럼 사이에 끼어들 수 있습니다.
        //     우리 서버는 저장 작업자 하나가 판을 차례로 처리하므로 괜찮습니다.
        //     최신 Redis(6.2+)는 ZADD 키 GT 점수 멤버 — "클 때만" 을 명령 하나로 합니다.
        //     MSOpenTech Redis(3.0)는 GT 를 모릅니다 → 마지막에 직접 확인합니다.
        //
        //   ★ ZADD 가 돌려주는 bool 은 "새 멤버가 들어갔나" 입니다.
        //     이미 있던 멤버의 점수를 바꿔도 false.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step6_BestOnlyAsync()
        {
            await _db.KeyDeleteAsync("lab:best");
            await _db.SortedSetAddAsync("lab:best", "홍길동", 23);
            Console.WriteLine("  시작 : 홍길동 23점");

            // ✗ 그냥 ZADD
            Cmd("ZADD lab:best 15 홍길동      (이번 판 15점 — 그냥 넣으면)");
            bool a = await _db.SortedSetAddAsync("lab:best", "홍길동", 15);
            Console.WriteLine($"  → 반환 {a} · 점수 {await _db.SortedSetScoreAsync("lab:best", "홍길동")}   ← 최고 점수 23 이 사라짐!");

            // ✓ 읽고 → 비교하고 → 클 때만
            await _db.SortedSetAddAsync("lab:best", "홍길동", 23);
            Console.WriteLine();
            Console.WriteLine("  다시 23점으로 되돌리고, 이번엔 비교해서 넣습니다.");
            foreach ((string who, int score) in new[] { ("홍길동", 15), ("홍길동", 40), ("신입", 0) })
            {
                Cmd($"ZSCORE lab:best {who}   → 비교 → (크면) ZADD lab:best {score} {who}");
                bool updated = await UpdateBestAsync("lab:best", who, score);
                Console.WriteLine($"  이번 판 {score}점 → {(updated ? "갱신" : "그대로")} · 점수 {await _db.SortedSetScoreAsync("lab:best", who)}");
            }

            // 반환값 함정
            Console.WriteLine();
            Cmd("ZADD lab:best 50 홍길동      (이미 있는 멤버의 점수를 올리면)");
            bool b = await _db.SortedSetAddAsync("lab:best", "홍길동", 50);
            Console.WriteLine($"  → 반환 {b} · 점수 {await _db.SortedSetScoreAsync("lab:best", "홍길동")}   ← 바뀌었는데 False! (\"새 멤버?\" 라는 뜻)");

            // 최신 Redis 의 GT
            Console.WriteLine();
            Cmd("ZADD lab:best GT 45 홍길동   (Redis 6.2+ 의 \"클 때만\" 옵션)");
            try
            {
                await _db.SortedSetAddAsync("lab:best", "홍길동", 45, SortedSetWhen.GreaterThan);
                Console.WriteLine($"  → 이 서버는 GT 를 압니다 · 점수 {await _db.SortedSetScoreAsync("lab:best", "홍길동")} (45 < 50 이라 그대로)");
            }
            catch (RedisServerException ex)
            {
                Console.WriteLine($"  → {ex.Message}   ← 이 서버는 GT 를 모릅니다 (Redis 6.2 부터)");
            }
        }

        /// <summary>6번 · 서버 TODO 2 와 같은 코드 — 더 클 때만 넣고, 넣었으면 true</summary>
        private static async Task<bool> UpdateBestAsync(string key, string member, int score)
        {
            double? now = await _db.SortedSetScoreAsync(key, member);
            if (now == null || score > now.Value)
            {
                await _db.SortedSetAddAsync(key, member, score);
                return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        // 7. 없는 값은 null
        //
        //   Redis 는 없으면 nil 을 줍니다. C# 에서는
        //     GET           → RedisValue.IsNull == true
        //     ZREVRANK      → long?   null
        //     ZSCORE        → double? null
        //
        //   8주차 "줄이 없음(ReadAsync false)" · "칸이 NULL(IsDBNull)" 과 같은 이야기입니다.
        //   null 확인 없이 .Value 를 쓰면 InvalidOperationException.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step7_NullAsync()
        {
            Cmd("GET lab:없는키");
            RedisValue v = await _db.StringGetAsync("lab:없는키");
            Console.WriteLine($"  → IsNull {v.IsNull} · HasValue {v.HasValue}");

            await _db.SortedSetAddAsync("lab:rank", "홍길동", 23);

            Cmd("ZREVRANK lab:rank 박민수      (판을 한 번도 안 한 사람)");
            long? r = await _db.SortedSetRankAsync("lab:rank", "박민수", Order.Descending);
            Console.WriteLine($"  → {(r == null ? "null" : r.ToString())}");

            Cmd("ZSCORE lab:rank 박민수");
            double? s = await _db.SortedSetScoreAsync("lab:rank", "박민수");
            Console.WriteLine($"  → {(s == null ? "null" : s.ToString())}");

            Console.WriteLine();
            Console.WriteLine("  null 확인 없이 r.Value 를 쓰면?");
            try
            {
                long boom = r!.Value;
                Console.WriteLine($"  → {boom}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"  → {ex.GetType().Name} : {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 8. 속도 비교 — 플레이어 10만 명
        //
        //   먼저 Workbench 에서 root 로 Database/02_bench_seed.sql 실행 (가짜 10만 명)
        //   끝나면 03_bench_clean.sql 로 지웁니다.
        //
        //   비교하는 것 : "bot050000 은 몇 등인가?"
        //     MySQL   SELECT COUNT(*) + 1 FROM players WHERE best_score > @s
        //             → 나보다 높은 사람을 전부 세야 함 (인덱스가 있어도 수만 개를 셈)
        //     Redis   ZREVRANK lab:bench bot050000
        //             → 정렬된 상태로 들고 있어서 바로 나옴
        // ═══════════════════════════════════════════════════════════
        private static async Task Step8_SpeedAsync()
        {
            const string WHO = "bot050000";
            const int REPEAT = 100;

            await using var conn = new MySqlConnection(MYSQL);
            await conn.OpenAsync();

            // ── 준비 : MySQL → Redis 로 옮기기 (서버의 RebuildAsync 와 같은 일) ──
            Console.WriteLine("  MySQL 의 전체 플레이어를 Redis(lab:bench)로 옮기는 중...");
            var sw = Stopwatch.StartNew();
            await _db.KeyDeleteAsync("lab:bench");

            var batch = new List<SortedSetEntry>(1000);
            long total = 0;
            await using (var cmd = new MySqlCommand("SELECT nickname, best_score FROM players", conn))
            await using (MySqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    batch.Add(new SortedSetEntry(reader.GetString(0), reader.GetInt32(1)));
                    if (batch.Count == 1000)
                    {
                        await _db.SortedSetAddAsync("lab:bench", batch.ToArray());
                        total += batch.Count;
                        batch.Clear();
                    }
                }
            }
            if (batch.Count > 0)
            {
                await _db.SortedSetAddAsync("lab:bench", batch.ToArray());
                total += batch.Count;
            }
            Console.WriteLine($"  → {total:N0}명 · {sw.ElapsedMilliseconds}ms");

            double? score = await _db.SortedSetScoreAsync("lab:bench", WHO);
            if (score == null)
            {
                Console.WriteLine($"  {WHO} 가 없습니다. Database/02_bench_seed.sql 을 root 로 먼저 실행하세요.");
                return;
            }
            Console.WriteLine($"  {WHO} 의 최고 점수 : {score}점");
            Console.WriteLine();

            // ── MySQL ──
            long mysqlRank = 0;
            sw.Restart();
            for (int i = 0; i < REPEAT; i++)
            {
                await using var cmd = new MySqlCommand(
                    "SELECT COUNT(*) + 1 FROM players WHERE best_score > @s", conn);
                cmd.Parameters.AddWithValue("@s", (int)score.Value);
                mysqlRank = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }
            double mysqlMs = sw.Elapsed.TotalMilliseconds / REPEAT;

            // ── Redis ──
            long redisRank = 0;
            sw.Restart();
            for (int i = 0; i < REPEAT; i++)
            {
                long? r = await _db.SortedSetRankAsync("lab:bench", WHO, Order.Descending);
                redisRank = r!.Value + 1;
            }
            double redisMs = sw.Elapsed.TotalMilliseconds / REPEAT;

            Console.WriteLine($"  \"{WHO} 은 몇 등?\" — {REPEAT}번 평균");
            Console.WriteLine($"    MySQL  COUNT(*)   {mysqlRank,7:N0}위   {mysqlMs,8:0.000} ms");
            Console.WriteLine($"    Redis  ZREVRANK   {redisRank,7:N0}위   {redisMs,8:0.000} ms");
            Console.WriteLine($"    → Redis 가 약 {mysqlMs / redisMs:0}배 빠름");
            Console.WriteLine();
            Console.WriteLine("  순위가 조금 다른 이유 : 같은 점수가 여러 명일 때");
            Console.WriteLine("    MySQL COUNT(*)+1 은 \"나보다 높은 사람 수 + 1\" → 같은 점수는 공동 순위");
            Console.WriteLine("    Redis 는 같은 점수끼리 멤버 이름 순서로 줄을 세움 → 모두 다른 순위");

            // ── TOP 10 도 비교 ──
            sw.Restart();
            for (int i = 0; i < REPEAT; i++)
            {
                await using var cmd = new MySqlCommand(
                    "SELECT nickname, best_score FROM players ORDER BY best_score DESC, id LIMIT 10", conn);
                await using MySqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            }
            double mysqlTop = sw.Elapsed.TotalMilliseconds / REPEAT;

            sw.Restart();
            for (int i = 0; i < REPEAT; i++)
                await _db.SortedSetRangeByRankWithScoresAsync("lab:bench", 0, 9, Order.Descending);
            double redisTop = sw.Elapsed.TotalMilliseconds / REPEAT;

            Console.WriteLine();
            Console.WriteLine($"  \"TOP 10\" — {REPEAT}번 평균");
            Console.WriteLine($"    MySQL  ORDER BY ... LIMIT 10   {mysqlTop,8:0.000} ms   (idx_rank 인덱스가 있으면 빠름)");
            Console.WriteLine($"    Redis  ZREVRANGE 0 9           {redisTop,8:0.000} ms");
        }

        /// <summary>지금 보내는 Redis 명령을 화면에 보여 줍니다</summary>
        private static void Cmd(string command)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  redis> {command}");
            Console.ResetColor();
        }
    }
}
