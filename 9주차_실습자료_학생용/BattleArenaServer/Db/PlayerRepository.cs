using System;
using System.Threading.Tasks;
using MySqlConnector;

namespace BattleArenaServer
{
    /// <summary>
    /// players 테이블의 한 줄
    ///
    /// ★ 테이블 한 줄 = C# 객체 하나
    ///   칸 이름 best_score → 속성 이름 BestScore
    /// </summary>
    public class PlayerRecord
    {
        public int    Id          { get; set; }
        public string Nickname    { get; set; } = "";
        public int    BestScore   { get; set; }
        public int    GamesPlayed { get; set; }
    }

    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  players 테이블을 다루는 곳
    ///
    ///  "Repository(저장소)" 는 SQL 을 한곳에 모아 두는 클래스입니다.
    ///  게임 코드(ClientSession, GameLoop)에는 SQL 이 한 줄도 없고,
    ///  여기 있는 함수만 부릅니다.
    ///
    ///    ClientSession      PlayerRepository.LoginAsync("홍길동")
    ///                                  ↓
    ///    PlayerRepository   SELECT / INSERT / UPDATE
    ///                                  ↓
    ///    MySQL
    ///
    ///  SQL 을 고칠 일이 생기면 이 파일만 보면 됩니다.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class PlayerRepository
    {
        /// <summary>
        /// 교수님 시연용 스위치 (학생 자료에는 없습니다)
        ///
        /// true 면 닉네임을 SQL 에 문자열로 붙입니다. ← 일부러 만든 구멍
        /// 실행할 때 --unsafesql 을 붙이면 켜집니다.
        /// 예제 G 7번(SQL 인젝션)이 뚫리는 걸 보여줄 때 씁니다.
        /// </summary>
        public static bool UnsafeSql = false;

        // ═══════════════════════════════════════
        // 로그인 = 찾기 → 없으면 만들기 → 접속 시각 기록
        // ═══════════════════════════════════════
        public static async Task<PlayerRecord> LoginAsync(string nickname)
        {
            // ═══════════════════════════════════════
            // [8주차 TODO 4 · 완성] 로그인 흐름 완성
            //
            //   1. FindByNicknameAsync 로 찾는다
            //   2. 없으면(null) CreateAsync 로 만든다
            //
            //      ★ 같은 닉네임으로 두 명이 "동시에" 처음 들어오면?
            //        둘 다 1번에서 null → 둘 다 INSERT → 한 명은 1062 에러
            //        UNIQUE 가 막아 준 것이니 당황하지 말고
            //        1062 를 잡아서 다시 찾으면 됩니다.
            //
            //        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            //
            //   3. TouchLoginAsync 로 접속 시각을 기록한다
            //   4. 찾은(또는 만든) PlayerRecord 를 돌려준다
            // ═══════════════════════════════════════
            PlayerRecord? player = await FindByNicknameAsync(nickname);

            if (player == null)
            {
                try
                {
                    player = await CreateAsync(nickname);
                    Console.WriteLine($"[DB] 새 플레이어 가입 : {nickname} (id {player.Id})");
                }
                catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
                {
                    // 그 사이 누군가 같은 닉네임으로 가입했습니다 → 다시 찾습니다
                    player = await FindByNicknameAsync(nickname)
                             ?? throw new InvalidOperationException("가입 직후인데 플레이어를 못 찾았습니다.");
                }
            }

            await TouchLoginAsync(player.Id);
            return player;
        }

        // ═══════════════════════════════════════
        // 닉네임으로 찾기 — 없으면 null
        //
        //   SQL   SELECT id, nickname, best_score, games_played
        //         FROM players WHERE nickname = @nick
        //
        //   예제 I 5번과 같은 모양입니다.
        // ═══════════════════════════════════════
        public static async Task<PlayerRecord?> FindByNicknameAsync(string nickname)
        {
            // ═══════════════════════════════════════
            // [8주차 TODO 2 · 완성] SELECT 한 줄 → PlayerRecord
            //
            //   1. 연결 열기             await using var conn = await Db.OpenAsync();
            //   2. SQL 담기              @nick 빈칸을 쓰세요 (따옴표 없이)
            //   3. 빈칸 채우기           cmd.Parameters.AddWithValue("@nick", nickname);
            //   4. ExecuteReaderAsync() → ReadAsync() 가 false 면 return null
            //   5. reader.GetInt32(0), GetString(1) ... 로 PlayerRecord 를 만들어 돌려주기
            //
            //   ★ "... WHERE nickname = '" + nickname + "'" 처럼 붙이면 안 됩니다
            //     예제 G 7번이 그 구멍을 공격합니다.
            // ═══════════════════════════════════════
            await using MySqlConnection conn = await Db.OpenAsync();

            MySqlCommand cmd;
            if (UnsafeSql)
            {
                // ✗ 시연용 구멍 : 사용자가 보낸 글자가 SQL 의 일부가 됩니다
                cmd = new MySqlCommand(
                    "SELECT id, nickname, best_score, games_played " +
                    "FROM players WHERE nickname = '" + nickname + "'", conn);
            }
            else
            {
                // ✓ 파라미터 : 사용자가 보낸 글자는 끝까지 "값" 입니다
                cmd = new MySqlCommand(
                    "SELECT id, nickname, best_score, games_played " +
                    "FROM players WHERE nickname = @nick", conn);
                cmd.Parameters.AddWithValue("@nick", nickname);
            }

            await using (cmd)
            {
                await using MySqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                return new PlayerRecord
                {
                    Id          = reader.GetInt32(0),     // id           INT
                    Nickname    = reader.GetString(1),    // nickname     VARCHAR(20)
                    BestScore   = reader.GetInt32(2),     // best_score   INT
                    GamesPlayed = reader.GetInt32(3),     // games_played INT
                };
            }
        }

        // ═══════════════════════════════════════
        // 새 플레이어 만들기
        //
        //   SQL   INSERT INTO players (nickname) VALUES (@nick)
        //
        //   best_score · total_score · games_played 는 DEFAULT 0 이라 안 적어도 됩니다.
        //   예제 I 6번과 같은 모양입니다.
        // ═══════════════════════════════════════
        public static async Task<PlayerRecord> CreateAsync(string nickname)
        {
            // ═══════════════════════════════════════
            // [8주차 TODO 3 · 완성] INSERT → 새 id
            //
            //   1. 연결 열기 · SQL 담기 · 빈칸 채우기 (8주차 TODO 2 와 같음)
            //   2. 결과 표가 없으니 ExecuteNonQueryAsync()
            //   3. 새 id 는 cmd.LastInsertedId  (long 이라서 (int) 로 바꿈)
            //   4. new PlayerRecord { Id = 새 id, Nickname = nickname } 을 돌려주기
            //
            //   같은 닉네임이 이미 있으면 여기서 MySqlException(1062)이 납니다.
            //   잡는 건 8주차 TODO 4 에서 합니다. 여기서는 잡지 마세요.
            // ═══════════════════════════════════════
            await using MySqlConnection conn = await Db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "INSERT INTO players (nickname) VALUES (@nick)", conn);
            cmd.Parameters.AddWithValue("@nick", nickname);

            await cmd.ExecuteNonQueryAsync();

            return new PlayerRecord
            {
                Id       = (int)cmd.LastInsertedId,
                Nickname = nickname,
            };
        }

        // ═══════════════════════════════════════
        // 접속 시각 기록 (완성본 — UPDATE 예시로 읽어 보세요)
        //
        //   SQL   UPDATE players SET last_login_at = NOW() WHERE id = @id
        //
        //   UPDATE 는 ExecuteNonQueryAsync. 돌려받는 숫자는 "바뀐 줄 수" 입니다.
        //   WHERE 에 기본키(id)를 쓰니 1 이 나와야 정상입니다.
        // ═══════════════════════════════════════
        public static async Task TouchLoginAsync(int playerId)
        {
            await using MySqlConnection conn = await Db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "UPDATE players SET last_login_at = NOW() WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", playerId);

            int changed = await cmd.ExecuteNonQueryAsync();
            if (changed != 1)
                Console.WriteLine($"[DB] 경고 : 접속 시각 기록이 {changed}줄 바뀌었습니다 (id {playerId})");
        }
    }
}
