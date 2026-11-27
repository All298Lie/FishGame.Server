using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  랭킹 조회
    ///
    ///  서버 콘솔에서 rankdb 를 치면 TOP 10 을 출력합니다. (9주차부터 rank 는 Redis)
    ///
    ///  SQL   SELECT nickname, best_score, games_played
    ///        FROM players
    ///        ORDER BY best_score DESC, id
    ///        LIMIT @n
    ///
    ///  예제 I 3번(여러 줄 읽기)과 같은 모양입니다.
    ///  ★ 9주차 정정 : best_score 인덱스(idx_best_score)만으로는 이 쿼리가 인덱스를 못 씁니다.
    ///    DESC 와 ASC(id) 가 섞여 있어서 전체를 읽어 정렬합니다 (10만 명에서 약 22ms).
    ///    00_setup.sql 의 idx_rank (best_score DESC, id) 가 있어야 10줄만 읽습니다 (약 0.3ms).
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class RankingRepository
    {
        public class RankRow
        {
            public string Nickname    { get; set; } = "";
            public int    BestScore   { get; set; }
            public int    GamesPlayed { get; set; }
        }

        /// <summary>[9주차] Redis 다시 채우기용 — 한 판이라도 한 사람의 최고 점수</summary>
        public class ScoreRow
        {
            public string Nickname  { get; set; } = "";
            public int    BestScore { get; set; }
        }

        /// <summary>
        /// [9주차] 전체 최고 점수 (완성본)
        ///
        ///   SELECT nickname, best_score FROM players WHERE games_played > 0
        ///
        ///   판을 한 번도 안 한 사람(박민수)은 랭킹에 넣지 않습니다.
        /// </summary>
        public static async Task<List<ScoreRow>> AllBestScoresAsync()
        {
            var list = new List<ScoreRow>();

            await using MySqlConnection conn = await Db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "SELECT nickname, best_score FROM players WHERE games_played > 0", conn);

            await using MySqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ScoreRow
                {
                    Nickname  = reader.GetString(0),
                    BestScore = reader.GetInt32(1),
                });
            }
            return list;
        }

        public static async Task<List<RankRow>> TopAsync(int n)
        {
            // ═══════════════════════════════════════
            // [8주차 TODO 8 · 완성] 여러 줄 읽기 → List<RankRow>
            //
            //   while (await reader.ReadAsync()) { list.Add(...); }
            //   LIMIT 에도 파라미터(@n)를 쓸 수 있습니다.
            // ═══════════════════════════════════════
            var list = new List<RankRow>();

            await using MySqlConnection conn = await Db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "SELECT nickname, best_score, games_played " +
                "FROM players ORDER BY best_score DESC, id LIMIT @n", conn);
            cmd.Parameters.AddWithValue("@n", n);

            await using MySqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RankRow
                {
                    Nickname    = reader.GetString(0),
                    BestScore   = reader.GetInt32(1),
                    GamesPlayed = reader.GetInt32(2),
                });
            }

            return list;
        }
    }
}
