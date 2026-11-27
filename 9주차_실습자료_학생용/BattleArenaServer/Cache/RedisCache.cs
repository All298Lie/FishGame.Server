using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  [9주차] Redis 연결 담당
    ///
    ///  ★ MySQL 과 반대 — 연결 하나를 서버 전체가 같이 씁니다
    ///
    ///    MySQL   함수마다 열고 닫기   await using var conn = await Db.OpenAsync();
    ///    Redis   서버 시작 때 한 번   ConnectionMultiplexer 하나를 끝까지 공유
    ///
    ///    8주차에 "연결 하나를 같이 쓰면 already in use" 라고 했는데 왜 괜찮나?
    ///    ConnectionMultiplexer 는 여러 쓰레드의 명령을 한 줄로 세워서 보내고(파이프라이닝),
    ///    돌아오는 답을 순서대로 짝지어 돌려줍니다. 같이 쓰라고 만든 물건입니다.
    ///    오히려 명령마다 새로 만들면 느리고 연결이 쌓입니다.
    ///
    ///  ★ Redis 가 없어도 서버는 뜹니다
    ///
    ///    랭킹은 MySQL 에서 읽고(느리지만 정확), 판 저장도 그대로 됩니다.
    ///    Redis 는 "빠른 사본" 일 뿐 원본은 MySQL 입니다.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class RedisCache
    {
#pragma warning disable CS0649   // TODO 1 을 채우면 _mux 에 값이 들어갑니다
        private static ConnectionMultiplexer? _mux;
#pragma warning restore CS0649

        /// <summary>Redis 를 쓸 수 있는 상태인가</summary>
        public static bool IsAvailable => _mux != null && _mux.IsConnected;

        /// <summary>명령을 보낼 때 쓰는 창구 (가볍습니다. 필요할 때마다 받아 써도 됩니다)</summary>
        public static IDatabase Db0 => _mux!.GetDatabase();

        /// <summary>끊겼다가 다시 붙었을 때 (Redis 를 재시작한 경우 등)</summary>
        public static event Action? Reconnected;

        public static async Task<bool> ConnectAsync()
        {
            string config = BattleArenaServer.Db.RedisConfig;
            if (config.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Redis] dbsettings.json 에 \"Redis\" 항목이 없습니다. Redis 없이 시작합니다.");
                Console.ResetColor();
                return false;
            }

            try
            {
                // ═══════════════════════════════════════
                // TODO 1 : Redis 연결 확인
                //
                //   1. ConnectionMultiplexer.ConnectAsync(설정) 으로 연결한다
                //        설정 = "127.0.0.1:6379,connectTimeout=3000,abortConnect=false"
                //   2. GetDatabase().PingAsync() 로 왕복 시간을 잰다
                //   3. "[Redis] 연결 성공 — PING 0.3ms" 를 출력한다
                //   4. _mux 에 저장한다   ← 이게 있어야 랭킹이 Redis 를 씁니다
                //
                //   abortConnect=false : 처음에 못 붙어도 예외로 죽지 않고
                //                        뒤에서 계속 다시 붙으려고 합니다.
                //                        그래서 IsConnected 로 붙었는지 확인합니다.
                // ═══════════════════════════════════════

                _mux = await ConnectionMultiplexer.ConnectAsync(config);
                TimeSpan t = await _mux.GetDatabase().PingAsync();

                Console.WriteLine($"[Redis] 연결 성공 - PING {t.Milliseconds}ms");

                return _mux.IsConnected;
            }
            catch (NotImplementedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Redis] 연결 실패 — {ex.Message}");
                Console.WriteLine("        Redis 없이 시작합니다. 랭킹은 MySQL 에서 읽습니다.");
                Console.ResetColor();
                return false;
            }
        }

        /// <summary>끊김 · 재연결을 로그로 남기고, 재연결되면 알려 줍니다</summary>
        private static void HookEvents(ConnectionMultiplexer mux)
        {
            mux.ConnectionFailed += (_, e) =>
            {
                if (e.ConnectionType != ConnectionType.Interactive) return;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Redis] 연결 끊김 ({e.FailureType}) — 랭킹은 MySQL 로 대신합니다.");
                Console.ResetColor();
            };

            mux.ConnectionRestored += (_, e) =>
            {
                if (e.ConnectionType != ConnectionType.Interactive) return;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Redis] 다시 연결됨");
                Console.ResetColor();
                Reconnected?.Invoke();
            };
        }
    }
}
