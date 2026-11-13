using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MySqlConnector;

namespace SqlLab
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════
    ///  예제 I — SQL 을 C# 으로 옮기기
    ///
    ///  7주차에 Workbench 로 쳤던 SQL 을 C# 코드로 옮기는 연습장입니다.
    ///  완성본이니 고치지 말고, 메뉴를 하나씩 실행하면서 아래 코드를 읽으세요.
    ///
    ///  ── 모든 단계가 같은 다섯 줄 모양입니다 ──
    ///
    ///      1. 연결을 연다          new MySqlConnection(연결문자열) → OpenAsync()
    ///      2. SQL 을 담는다        new MySqlCommand(sql, conn)
    ///      3. 빈칸을 채운다        cmd.Parameters.AddWithValue("@이름", 값)
    ///      4. 실행한다             아래 셋 중 하나
    ///      5. 결과를 C# 값으로     reader.GetInt32(...) · GetString(...)
    ///
    ///  ── 실행 방법은 SQL 종류에 따라 셋 중 하나 ──
    ///
    ///      SQL                         C# 메서드                 돌려받는 것
    ///      ─────────────────────────   ───────────────────────   ─────────────
    ///      SELECT (여러 칸 · 여러 줄)   ExecuteReaderAsync()      한 줄씩 읽는 reader
    ///      SELECT (값 딱 하나)          ExecuteScalarAsync()      object 하나
    ///      INSERT · UPDATE · DELETE    ExecuteNonQueryAsync()    바뀐 줄 수 (int)
    ///
    ///  ── 먼저 할 것 ──
    ///      Database/00_setup.sql 을 Workbench 에서 root 로 실행
    ///      (battlearena_game DB 와 ba_server 계정이 만들어집니다)
    /// ═══════════════════════════════════════════════════════════
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// 연결 문자열 — "어디에, 누구로, 어떤 DB 에" 를 한 줄로 적은 것
        ///
        ///   Server    MySQL 이 있는 곳 (7주차 Workbench 접속의 Host)
        ///   Port      3306
        ///   Database  USE battlearena_game; 과 같은 뜻
        ///   User ID   ★ root 가 아니라 게임 서버 전용 계정
        ///   Password  00_setup.sql 에서 정한 비밀번호
        ///
        /// ★ 실습용이라 코드에 적었습니다.
        ///   실제 게임 서버는 dbsettings.json 에 따로 두고 GitHub 에 올리지 않습니다.
        /// </summary>
        private const string CONN =
            "Server=127.0.0.1;Port=3306;Database=battlearena_game;" +
            "User ID=ba_server;Password=ba-server-2026;";

        public static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding  = Encoding.UTF8;

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════════════╗");
                Console.WriteLine("║   예제 I — SQL 을 C# 으로 옮기기               ║");
                Console.WriteLine("╚══════════════════════════════════════════════╝");
                Console.WriteLine("  1. 연결하고 값 하나 받기     SELECT VERSION()");
                Console.WriteLine("  2. 한 줄 읽기               SELECT ... WHERE id = 1");
                Console.WriteLine("  3. 여러 줄 읽기             랭킹 TOP 10");
                Console.WriteLine("  4. 집계 값과 C# 타입         COUNT · SUM · AVG");
                Console.WriteLine("  5. 빈칸 채우기 (파라미터)    닉네임으로 찾기");
                Console.WriteLine("  6. 넣기와 새 번호            INSERT → LastInsertedId");
                Console.WriteLine("  7. 고치기와 NULL             UPDATE → 바뀐 줄 수");
                Console.WriteLine("  8. SQL 인젝션               붙이기 vs 파라미터");
                Console.WriteLine("  9. 트랜잭션                 판 결과 저장 (성공 / 실패)");
                Console.WriteLine("  0. 종료");
                Console.Write("  > ");

                string? key = Console.ReadLine()?.Trim();
                Console.WriteLine();

                try
                {
                    switch (key)
                    {
                        case "1": await Step1_ConnectAsync();     break;
                        case "2": await Step2_ReadOneAsync();     break;
                        case "3": await Step3_ReadManyAsync();    break;
                        case "4": await Step4_AggregateAsync();   break;
                        case "5": await Step5_ParameterAsync();   break;
                        case "6": await Step6_InsertAsync();      break;
                        case "7": await Step7_UpdateAsync();      break;
                        case "8": await Step8_InjectionAsync();   break;
                        case "9": await Step9_TransactionAsync(); break;
                        case "0": case null: return;
                        default:  Console.WriteLine("  번호를 다시 고르세요."); break;
                    }
                }
                catch (MySqlException ex)
                {
                    // MySQL 이 돌려준 에러 — 번호가 7주차에 본 그 번호입니다
                    Console.WriteLine($"  [MySQL 에러 {ex.Number}] {ex.Message}");
                    Console.WriteLine("  7주차 워크북 \"에러 번호로 찾기\" 표를 보세요.");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 1. 연결하고 값 하나 받기
        //
        //   Workbench                 C#
        //   ───────────────────────   ───────────────────────────────
        //   접속 상자 클릭            conn.OpenAsync()
        //   SELECT VERSION(); 실행    cmd.ExecuteScalarAsync()
        //   결과 칸 하나              object → string 으로 변환
        // ═══════════════════════════════════════════════════════════
        private static async Task Step1_ConnectAsync()
        {
            Sql("SELECT VERSION(), CURRENT_USER()");

            // ★ await using : 블록이 끝나면 연결을 자동으로 닫습니다
            //   닫지 않으면 연결이 계속 쌓여 결국 MySQL 이 접속을 거부합니다
            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            // 값이 딱 하나면 ExecuteScalarAsync — 첫 줄의 첫 칸만 돌려줍니다
            await using var cmd = new MySqlCommand("SELECT VERSION()", conn);
            object? version = await cmd.ExecuteScalarAsync();

            cmd.CommandText = "SELECT CURRENT_USER()";
            object? user = await cmd.ExecuteScalarAsync();

            Console.WriteLine($"  MySQL 버전   : {version}");
            Console.WriteLine($"  접속한 계정  : {user}");
            Console.WriteLine();
            Console.WriteLine("  → root 가 아니라 ba_server 로 접속했습니다.");
            Console.WriteLine("    이 계정은 SELECT · INSERT · UPDATE 만 할 수 있습니다.");
        }

        // ═══════════════════════════════════════════════════════════
        // 2. 한 줄 읽기
        //
        //   결과 표의 "칸" 을 C# 변수로 옮기는 단계입니다.
        //
        //   SQL 타입        C# 으로 읽기               C# 타입
        //   ─────────────   ────────────────────────   ─────────
        //   INT             reader.GetInt32(번호)      int
        //   BIGINT          reader.GetInt64(번호)      long
        //   VARCHAR(20)     reader.GetString(번호)     string
        //   DATETIME        reader.GetDateTime(번호)   DateTime
        //
        //   번호는 SELECT 에 적은 순서입니다. 0 부터 셉니다.
        //     SELECT id, nickname, best_score, games_played
        //            0   1         2           3
        // ═══════════════════════════════════════════════════════════
        private static async Task Step2_ReadOneAsync()
        {
            const string sql =
                "SELECT id, nickname, best_score, games_played " +
                "FROM players WHERE id = 1";
            Sql(sql);

            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(sql, conn);

            // 여러 칸을 읽을 때는 ExecuteReaderAsync
            await using MySqlDataReader reader = await cmd.ExecuteReaderAsync();

            // ReadAsync() 를 불러야 "첫 줄" 로 넘어갑니다.
            // 줄이 없으면 false — WHERE 에 맞는 행이 없다는 뜻입니다.
            if (!await reader.ReadAsync())
            {
                Console.WriteLine("  id = 1 인 플레이어가 없습니다.");
                return;
            }

            // ★ 결과 한 줄 → C# 객체 하나
            var p = new PlayerRow
            {
                Id          = reader.GetInt32(0),     // id           INT
                Nickname    = reader.GetString(1),    // nickname     VARCHAR
                BestScore   = reader.GetInt32(2),     // best_score   INT
                GamesPlayed = reader.GetInt32(3),     // games_played INT
            };

            Console.WriteLine($"  읽은 결과 → PlayerRow {{ Id={p.Id}, Nickname={p.Nickname}, " +
                              $"BestScore={p.BestScore}, GamesPlayed={p.GamesPlayed} }}");
            Console.WriteLine();
            Console.WriteLine("  칸 이름으로 읽을 수도 있습니다 : reader.GetString(\"nickname\")");
            Console.WriteLine("  번호보다 느리지만, SELECT 순서를 바꿔도 안 틀립니다.");
        }

        // ═══════════════════════════════════════════════════════════
        // 3. 여러 줄 읽기 — 랭킹 TOP 10
        //
        //   한 줄 읽기를 while 로 감싸면 됩니다.
        //
        //     while (await reader.ReadAsync())   ← 다음 줄이 있는 동안
        //     {
        //         한 줄을 객체로 만들어 리스트에 넣는다
        //     }
        //
        //   결과 표 전체 → List<PlayerRow>
        // ═══════════════════════════════════════════════════════════
        private static async Task Step3_ReadManyAsync()
        {
            const string sql =
                "SELECT id, nickname, best_score, games_played " +
                "FROM players ORDER BY best_score DESC, id LIMIT 10";
            Sql(sql);

            var ranking = new List<PlayerRow>();

            await using (var conn = new MySqlConnection(CONN))
            {
                await conn.OpenAsync();
                await using var cmd = new MySqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ranking.Add(new PlayerRow
                    {
                        Id          = reader.GetInt32(0),
                        Nickname    = reader.GetString(1),
                        BestScore   = reader.GetInt32(2),
                        GamesPlayed = reader.GetInt32(3),
                    });
                }
            }
            // ↑ 여기서 연결은 이미 닫혔습니다. 데이터는 리스트에 남아 있습니다.

            Console.WriteLine($"  List<PlayerRow> 에 {ranking.Count}명을 담았습니다.");
            Console.WriteLine();
            for (int i = 0; i < ranking.Count; i++)
                Console.WriteLine($"  {i + 1,2}위  {ranking[i].Nickname,-10}  {ranking[i].BestScore,4}점  ({ranking[i].GamesPlayed}판)");
        }

        // ═══════════════════════════════════════════════════════════
        // 4. 집계 값과 C# 타입 — 자주 틀리는 곳
        //
        //   집계 함수는 칸 타입과 다른 타입을 돌려줍니다.
        //
        //   SQL                     MySQL 결과 타입   C# 에서 받는 타입
        //   ─────────────────────   ───────────────   ─────────────────
        //   COUNT(*)                BIGINT            long
        //   SUM(INT 칸)             DECIMAL           decimal
        //   AVG(INT 칸)             DECIMAL           decimal
        //   MAX(INT 칸)             INT               int
        //
        //   (int)cmd.ExecuteScalarAsync() 처럼 바로 형변환하면
        //   InvalidCastException 이 납니다. Convert.ToXxx() 를 쓰세요.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step4_AggregateAsync()
        {
            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand("", conn);

            cmd.CommandText = "SELECT COUNT(*) FROM players";
            Sql(cmd.CommandText);
            object? count = await cmd.ExecuteScalarAsync();
            Console.WriteLine($"  돌려받은 값 {count}  ·  C# 타입 {count?.GetType().Name}");

            cmd.CommandText = "SELECT SUM(total_score) FROM players";
            Sql(cmd.CommandText);
            object? sum = await cmd.ExecuteScalarAsync();
            Console.WriteLine($"  돌려받은 값 {sum}  ·  C# 타입 {sum?.GetType().Name}");

            cmd.CommandText = "SELECT AVG(best_score) FROM players";
            Sql(cmd.CommandText);
            object? avg = await cmd.ExecuteScalarAsync();
            Console.WriteLine($"  돌려받은 값 {avg}  ·  C# 타입 {avg?.GetType().Name}");

            // 안전하게 바꾸는 법
            long    playerCount = Convert.ToInt64(count);
            long    totalScore  = Convert.ToInt64(sum);
            double  average     = Convert.ToDouble(avg);

            Console.WriteLine();
            Console.WriteLine($"  Convert 로 바꾼 값 : 플레이어 {playerCount}명, 총점 {totalScore}, 평균 {average:F1}");

            // ★ 일부러 틀려 보기 : object(long) 을 int 로 바로 형변환
            try
            {
                int wrong = (int)count!;
                Console.WriteLine($"  (int)count = {wrong}");
            }
            catch (InvalidCastException)
            {
                Console.WriteLine("  (int)count 는 InvalidCastException — 상자 안이 long 이라서입니다.");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 5. 빈칸 채우기 — 파라미터
        //
        //   SQL 에 값을 직접 쓰지 않고 @이름 으로 빈칸을 둡니다.
        //   값은 따로 넘깁니다.
        //
        //     SQL   ... WHERE nickname = @nick
        //     C#    cmd.Parameters.AddWithValue("@nick", nickname);
        //
        //   ★ 따옴표를 붙이지 않습니다.  '@nick' (X)   @nick (O)
        //     빈칸이 문자열이라는 건 MySQL 이 압니다.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step5_ParameterAsync()
        {
            Console.Write("  찾을 닉네임 (예: 홍길동) > ");
            string nickname = Console.ReadLine()?.Trim() ?? "";

            const string sql =
                "SELECT id, nickname, best_score, games_played " +
                "FROM players WHERE nickname = @nick";
            Sql(sql);
            Console.WriteLine($"  @nick = \"{nickname}\"");
            Console.WriteLine();

            PlayerRow? p = await FindByNicknameAsync(nickname);

            if (p == null)
                Console.WriteLine("  없는 닉네임입니다. (ReadAsync 가 false)");
            else
                Console.WriteLine($"  찾았습니다 → Id={p.Id}, BestScore={p.BestScore}, GamesPlayed={p.GamesPlayed}");

            Console.WriteLine();
            Console.WriteLine("  ★ 이 함수 FindByNicknameAsync 가 게임 서버 로그인에 그대로 들어갑니다.");
        }

        /// <summary>
        /// 닉네임으로 플레이어 찾기 — 없으면 null
        ///
        /// 게임 서버 PlayerRepository.FindByNicknameAsync 와 같은 모양입니다.
        /// </summary>
        private static async Task<PlayerRow?> FindByNicknameAsync(string nickname)
        {
            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT id, nickname, best_score, games_played " +
                "FROM players WHERE nickname = @nick", conn);

            cmd.Parameters.AddWithValue("@nick", nickname);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PlayerRow
            {
                Id          = reader.GetInt32(0),
                Nickname    = reader.GetString(1),
                BestScore   = reader.GetInt32(2),
                GamesPlayed = reader.GetInt32(3),
            };
        }

        // ═══════════════════════════════════════════════════════════
        // 6. 넣기와 새 번호
        //
        //   INSERT 는 결과 표가 없습니다 → ExecuteNonQueryAsync
        //   돌려받는 int 는 "넣은 줄 수" 입니다.
        //
        //   AUTO_INCREMENT 로 생긴 새 id 는 cmd.LastInsertedId (long)
        //
        //   같은 닉네임이 있으면 MySQL 에러 1062 → C# 에서는 MySqlException
        //   ex.Number == 1062  또는  ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry
        // ═══════════════════════════════════════════════════════════
        private static async Task Step6_InsertAsync()
        {
            Console.Write("  새 닉네임 (20글자 이하, 이미 있는 이름도 넣어 보세요) > ");
            string nickname = Console.ReadLine()?.Trim() ?? "";

            const string sql = "INSERT INTO players (nickname) VALUES (@nick)";
            Sql(sql);
            Console.WriteLine($"  @nick = \"{nickname}\"");
            Console.WriteLine();

            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@nick", nickname);

            try
            {
                int rows  = await cmd.ExecuteNonQueryAsync();
                long newId = cmd.LastInsertedId;

                Console.WriteLine($"  넣은 줄 수 {rows}  ·  새 id {newId}");
                Console.WriteLine("  (번호가 건너뛰어도 정상입니다. 실패한 INSERT 도 번호를 하나 씁니다)");
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                // 7주차 STEP 3 의 ★ 에러가 C# 에서는 이렇게 잡힙니다
                Console.WriteLine($"  이미 있는 닉네임입니다. (MySQL 에러 {ex.Number})");
                Console.WriteLine("  → 게임 서버라면 \"이 닉네임으로 로그인\" 으로 처리합니다.");
            }
            catch (MySqlException ex) when (ex.Number == 1406)
            {
                Console.WriteLine($"  닉네임이 너무 깁니다. (MySQL 에러 {ex.Number} Data too long)");
                Console.WriteLine("  → 게임 서버는 DB 에 보내기 전에 길이를 먼저 검사합니다.");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 7. 고치기와 NULL
        //
        //   UPDATE 도 ExecuteNonQueryAsync. 돌려받는 int 는 "바뀐 줄 수".
        //   0 이면 WHERE 에 맞는 줄이 없었다는 뜻입니다 — 버그를 찾는 단서.
        //
        //   NULL 칸 읽기
        //     DB 의 NULL 은 C# 의 null 과 다릅니다.
        //     reader.GetDateTime() 을 NULL 칸에 부르면 예외가 납니다.
        //     먼저 reader.IsDBNull(번호) 로 확인하고, C# 쪽은 DateTime? 로 받습니다.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step7_UpdateAsync()
        {
            Console.Write("  로그인 처리할 닉네임 (예: 박민수 — 한 번도 로그인 안 한 사람) > ");
            string nickname = Console.ReadLine()?.Trim() ?? "";

            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            // ── 고치기 전 ──
            var (found, before) = await ReadLastLoginAsync(conn, nickname);
            if (found)
                Console.WriteLine($"  고치기 전 last_login_at : {Show(before)}");

            // ── 고치기 ──
            const string sql = "UPDATE players SET last_login_at = NOW() WHERE nickname = @nick";
            Sql(sql);

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@nick", nickname);
            int changed = await cmd.ExecuteNonQueryAsync();

            Console.WriteLine($"  바뀐 줄 수 : {changed}");
            if (changed == 0)
            {
                Console.WriteLine("  → 0 줄. 그런 닉네임이 없습니다. 에러가 아니라서 그냥 지나가니 꼭 확인하세요.");
                return;
            }

            // ── 고친 뒤 ──
            var (_, after) = await ReadLastLoginAsync(conn, nickname);
            Console.WriteLine($"  고친 뒤 last_login_at  : {Show(after)}");
        }

        /// <summary>
        /// NULL 일 수 있는 칸을 DateTime? 으로 읽기
        ///
        ///   found  그런 닉네임이 있는가 (줄이 있는가)
        ///   value  있다면 last_login_at — NULL 이면 null
        ///
        /// "줄이 없음" 과 "줄은 있는데 칸이 NULL" 은 다른 상황입니다.
        /// </summary>
        private static async Task<(bool found, DateTime? value)> ReadLastLoginAsync(
            MySqlConnection conn, string nickname)
        {
            await using var cmd = new MySqlCommand(
                "SELECT last_login_at FROM players WHERE nickname = @nick", conn);
            cmd.Parameters.AddWithValue("@nick", nickname);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return (false, null);     // 줄이 없음

            // ★ NULL 이면 GetDateTime 대신 null
            DateTime? value = reader.IsDBNull(0) ? null : reader.GetDateTime(0);
            return (true, value);
        }

        private static string Show(DateTime? t) => t == null ? "NULL" : t.Value.ToString("yyyy-MM-dd HH:mm:ss");

        // ═══════════════════════════════════════════════════════════
        // 8. SQL 인젝션 — 문자열로 붙이면 생기는 일
        //
        //   ✗  "... WHERE nickname = '" + nickname + "'"
        //   ✓  "... WHERE nickname = @nick"  +  AddWithValue("@nick", nickname)
        //
        //   붙이면 사용자가 입력한 글자가 "SQL 의 일부" 가 됩니다.
        //   파라미터는 입력이 끝까지 "값" 으로만 취급됩니다.
        //
        //   넣어 볼 입력
        //     홍길동                                              정상
        //     ' OR 1=1 #                                          전원이 나옴
        //     x'; UPDATE players SET best_score = 9999 WHERE id = 1; #
        //                                                         홍길동 점수 조작
        //     x'; DROP TABLE players; #                           권한이 막음 (1142)
        //
        //   ※ 3번째를 해 봤다면 00_setup.sql 을 다시 실행해서 되돌리세요.
        // ═══════════════════════════════════════════════════════════
        private static async Task Step8_InjectionAsync()
        {
            Console.WriteLine("  넣어 볼 입력 :  ' OR 1=1 #");
            Console.WriteLine("                  x'; UPDATE players SET best_score = 9999 WHERE id = 1; #");
            Console.WriteLine("                  x'; DROP TABLE players; #");
            Console.WriteLine();
            Console.Write("  닉네임 입력 > ");
            string input = Console.ReadLine() ?? "";

            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            // ── ✗ 붙이기 ──
            string unsafeSql = "SELECT id, nickname, best_score FROM players WHERE nickname = '" + input + "'";

            Console.WriteLine();
            Console.WriteLine("  ✗ 문자열로 붙인 경우 — MySQL 이 받은 SQL :");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    {unsafeSql}");
            Console.ResetColor();

            try
            {
                await using var cmd = new MySqlCommand(unsafeSql, conn);
                int n = await PrintRowsAsync(cmd);
                Console.WriteLine($"    → {n}줄이 나왔습니다.");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"    → MySQL 에러 {ex.Number} : {ex.Message}");
            }

            // ── ✓ 파라미터 ──
            const string safeSql = "SELECT id, nickname, best_score FROM players WHERE nickname = @nick";

            Console.WriteLine();
            Console.WriteLine("  ✓ 파라미터를 쓴 경우 — MySQL 이 받은 SQL :");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    {safeSql}");
            Console.WriteLine($"    @nick = \"{input}\"   ← 통째로 \"닉네임 값\" 하나");
            Console.ResetColor();

            await using (var cmd = new MySqlCommand(safeSql, conn))
            {
                cmd.Parameters.AddWithValue("@nick", input);
                int n = await PrintRowsAsync(cmd);
                Console.WriteLine($"    → {n}줄이 나왔습니다. 입력이 \"그런 닉네임이 있나\" 로만 쓰였습니다.");
            }

            // 홍길동 점수가 조작됐는지 확인
            await using (var check = new MySqlCommand("SELECT best_score FROM players WHERE id = 1", conn))
            {
                object? best = await check.ExecuteScalarAsync();
                Console.WriteLine();
                Console.WriteLine($"  지금 홍길동(id 1)의 best_score : {best}" +
                                  (Convert.ToInt32(best) == 9999 ? "   ← 조작되었습니다!" : ""));
            }
        }

        /// <summary>SELECT 결과를 그대로 출력하고 줄 수를 돌려줍니다</summary>
        private static async Task<int> PrintRowsAsync(MySqlCommand cmd)
        {
            int n = 0;
            await using var reader = await cmd.ExecuteReaderAsync();
            do
            {
                while (await reader.ReadAsync())
                {
                    n++;
                    var cols = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        cols.Add(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "");
                    Console.WriteLine("      " + string.Join(" | ", cols));
                }
            }
            while (await reader.NextResultAsync());   // 문장이 여러 개면 결과도 여러 개
            return n;
        }

        // ═══════════════════════════════════════════════════════════
        // 9. 트랜잭션 — 판 결과 저장
        //
        //   한 판을 저장하려면 SQL 세 종류를 실행합니다.
        //
        //     ① INSERT INTO matches ...          → 새 판 번호
        //     ② INSERT INTO match_results ...    → 참가자 수만큼
        //     ③ UPDATE players SET 통계 ...       → 참가자 수만큼
        //
        //   중간에 실패하면? ① 만 남고 ②·③ 은 없는 "반쪽 판" 이 생깁니다.
        //
        //   ※ 실패 모드에서 에러 번호가 1452 가 아니라 1216 으로 나옵니다.
        //     둘 다 "외래키 때문에 못 넣음" 입니다. root 로 Workbench 에서 하면 1452,
        //     권한을 줄인 ba_server 로 하면 제약 이름 같은 자세한 내용을 숨긴 1216 이 나옵니다.
        //   그래서 셋을 하나로 묶습니다.
        //
        //     SQL                      C#
        //     ─────────────────────    ─────────────────────────────────────
        //     START TRANSACTION;       var tx = await conn.BeginTransactionAsync();
        //     (각 SQL)                 new MySqlCommand(sql, conn, tx)   ← tx 를 꼭 넘김
        //     COMMIT;                  await tx.CommitAsync();
        //     ROLLBACK;                await tx.RollbackAsync();   (catch 안에서)
        // ═══════════════════════════════════════════════════════════
        private static async Task Step9_TransactionAsync()
        {
            Console.Write("  중간에 일부러 실패시킬까요? (y = 없는 플레이어 999번을 넣음) > ");
            bool fail = (Console.ReadLine()?.Trim().ToLower() ?? "") == "y";

            // 이번 판 결과 (게임 서버라면 GameLoop 가 만들어 줍니다)
            var results = new List<(int playerId, int score)>
            {
                (1, 18),                    // 홍길동
                (fail ? 999 : 2, 9),        // 김철수 — 실패 모드면 없는 999번
            };

            await using var conn = new MySqlConnection(CONN);
            await conn.OpenAsync();

            long before = await CountMatchesAsync(conn);
            Console.WriteLine($"  저장 전 matches : {before}판");
            Console.WriteLine();

            await using MySqlTransaction tx = await conn.BeginTransactionAsync();
            Console.WriteLine("  START TRANSACTION");

            try
            {
                // ① 판
                await using var insertMatch = new MySqlCommand(
                    "INSERT INTO matches (started_at, ended_at) VALUES (@start, @end)", conn, tx);
                insertMatch.Parameters.AddWithValue("@start", DateTime.Now.AddSeconds(-60));
                insertMatch.Parameters.AddWithValue("@end",   DateTime.Now);
                await insertMatch.ExecuteNonQueryAsync();

                long matchId = insertMatch.LastInsertedId;
                Console.WriteLine($"  ① INSERT matches          → 새 판 번호 {matchId}");

                int ranking = 1;
                foreach (var (playerId, score) in results)
                {
                    // ② 결과
                    await using var insertResult = new MySqlCommand(
                        "INSERT INTO match_results (match_id, player_id, score, ranking) " +
                        "VALUES (@m, @p, @s, @r)", conn, tx);
                    insertResult.Parameters.AddWithValue("@m", matchId);
                    insertResult.Parameters.AddWithValue("@p", playerId);
                    insertResult.Parameters.AddWithValue("@s", score);
                    insertResult.Parameters.AddWithValue("@r", ranking);
                    await insertResult.ExecuteNonQueryAsync();
                    Console.WriteLine($"  ② INSERT match_results    → 플레이어 {playerId}, {score}점");

                    // ③ 통계
                    await using var updatePlayer = new MySqlCommand(
                        "UPDATE players SET " +
                        "  total_score  = total_score + @s, " +
                        "  best_score   = GREATEST(best_score, @s), " +
                        "  games_played = games_played + 1 " +
                        "WHERE id = @p", conn, tx);
                    updatePlayer.Parameters.AddWithValue("@s", score);
                    updatePlayer.Parameters.AddWithValue("@p", playerId);
                    await updatePlayer.ExecuteNonQueryAsync();
                    Console.WriteLine($"  ③ UPDATE players          → 플레이어 {playerId} 통계");

                    ranking++;
                }

                await tx.CommitAsync();
                Console.WriteLine("  COMMIT  — 셋 다 확정");
            }
            catch (MySqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ 실패 : MySQL 에러 {ex.Number} — {ex.Message}");
                Console.ResetColor();

                await tx.RollbackAsync();
                Console.WriteLine("  ROLLBACK — ① 에서 넣은 판까지 전부 취소");
            }

            long after = await CountMatchesAsync(conn);
            Console.WriteLine();
            Console.WriteLine($"  저장 후 matches : {after}판   (" +
                              (after == before ? "그대로 — 반쪽 판이 남지 않았습니다" : $"+{after - before}") + ")");
        }

        private static async Task<long> CountMatchesAsync(MySqlConnection conn)
        {
            await using var cmd = new MySqlCommand("SELECT COUNT(*) FROM matches", conn);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        // ── 출력 도우미 ──
        private static void Sql(string sql)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  SQL> {sql}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// players 테이블의 한 줄을 담는 C# 클래스
    ///
    /// ★ 테이블 한 줄 = 객체 하나
    ///   칸 이름은 snake_case(best_score), C# 속성은 PascalCase(BestScore)
    /// </summary>
    public class PlayerRow
    {
        public int    Id          { get; set; }
        public string Nickname    { get; set; } = "";
        public int    BestScore   { get; set; }
        public int    GamesPlayed { get; set; }
    }
}
