using System;
using System.IO;
using System.Threading.Tasks;
using MySqlConnector;
using Newtonsoft.Json;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  DB 연결 담당
    ///
    ///  ★ 연결 문자열은 코드에 쓰지 않습니다
    ///
    ///    dbsettings.json 에서 읽습니다.
    ///    이 파일은 .gitignore 에 들어 있어서 GitHub 에 올라가지 않습니다.
    ///    비밀번호가 GitHub 에 올라가면 누구나 우리 DB 에 들어올 수 있습니다.
    ///
    ///    처음 받았을 때는 dbsettings.sample.json 을 복사해서
    ///    dbsettings.json 으로 이름을 바꾸고 비밀번호를 적으세요.
    ///
    ///  ★ 연결은 쓸 때 열고, 다 쓰면 바로 닫습니다
    ///
    ///    await using var conn = await Db.OpenAsync();
    ///
    ///    "매번 여는 건 느리지 않나?" — 아닙니다.
    ///    MySqlConnector 가 닫은 연결을 버리지 않고 모아 뒀다가(커넥션 풀)
    ///    다음 OpenAsync 때 다시 줍니다. 실제 TCP 연결은 몇 개만 유지됩니다.
    ///
    ///    반대로 연결 하나를 서버 전체가 같이 쓰면,
    ///    두 플레이어가 동시에 로그인할 때 한 연결에 두 명령이 섞여서 터집니다.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class Db
    {
        private const string SETTINGS_FILE = "dbsettings.json";

        /// <summary>dbsettings.json 에서 읽은 연결 문자열</summary>
        public static string ConnectionString { get; private set; } = "";

        /// <summary>DB 를 쓸 수 있는 상태인가 (시작할 때 확인)</summary>
        public static bool IsAvailable { get; private set; }

        private class Settings
        {
            public string ConnectionString { get; set; } = "";
        }

        /// <summary>설정 파일 읽기</summary>
        public static bool LoadSettings()
        {
            string path = Path.Combine(AppContext.BaseDirectory, SETTINGS_FILE);

            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DB] {SETTINGS_FILE} 가 없습니다.");
                Console.WriteLine("     dbsettings.sample.json 을 복사해서 dbsettings.json 으로 만들고");
                Console.WriteLine("     비밀번호를 적은 뒤 다시 빌드하세요.");
                Console.ResetColor();
                return false;
            }

            Settings? s = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            ConnectionString = s?.ConnectionString ?? "";
            return ConnectionString.Length > 0;
        }

        /// <summary>
        /// 연결을 하나 열어서 돌려줍니다
        ///
        /// 받는 쪽은 반드시 await using 으로 받으세요.
        /// </summary>
        public static async Task<MySqlConnection> OpenAsync()
        {
            var conn = new MySqlConnection(ConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>
        /// 서버 시작 때 DB 가 살아 있는지 확인
        ///
        /// 7주차 00_install_check.sql 의 첫 줄을 C# 으로 옮긴 것입니다.
        ///     SELECT VERSION();
        /// </summary>
        public static async Task<bool> CheckAsync()
        {
            if (!LoadSettings())
            {
                IsAvailable = false;
                return false;
            }

            try
            {
                // ═══════════════════════════════════════
                // TODO 1 : DB 연결 확인
                //
                //   SQL   SELECT VERSION()
                //   C#    값 하나 → ExecuteScalarAsync()
                //
                //   1. OpenAsync() 로 연결을 연다          (await using)
                //   2. MySqlCommand 에 SQL 을 담는다       (await using)
                //   3. ExecuteScalarAsync() 로 실행한다
                //   4. "[DB] 연결 성공 — MySQL 8.4.x" 를 출력한다
                //   5. IsAvailable = true;   ← 이게 있어야 로그인 · 저장이 DB 를 씁니다
                //
                //   실패하면 MySqlException 이 나고, 아래 catch 가 이유를 알려 줍니다.
                // ═══════════════════════════════════════
                // ── 여기에 작성 ──

                // 1. DB 연결
                await using var conn = await OpenAsync();

                // 2. SQL문 실행
                await using var cmd = new MySqlCommand("SELECT VERSION()", conn);

                // 3. 데이터 반환
                object? version = await cmd.ExecuteScalarAsync();

                // 4. 메세지 출력
                Console.WriteLine($"[DB] 연결 성공 - MYSQL {version}");

                // 5. DB 사용을 위한 프로퍼티 값 변경
                IsAvailable = true;
            }
            catch (MySqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DB] 연결 실패 ({ex.Number}) {ex.Message}");
                Console.WriteLine(ex.Number switch
                {
                    1045 => "     → 아이디나 비밀번호가 틀렸습니다. dbsettings.json 을 확인하세요.",
                    1049 => "     → DB 가 없습니다. Database/00_setup.sql 을 먼저 실행하세요.",
                    1042 => "     → MySQL 서버가 꺼져 있습니다. 서비스(MySQL84)를 확인하세요.",
                    _    => "     → 7주차 워크북의 에러 번호 표를 확인하세요.",
                });
                Console.WriteLine("     DB 없이 시작합니다. 게임은 되지만 결과는 저장되지 않습니다.");
                Console.ResetColor();

                IsAvailable = false;
            }

            return IsAvailable;
        }
    }
}
