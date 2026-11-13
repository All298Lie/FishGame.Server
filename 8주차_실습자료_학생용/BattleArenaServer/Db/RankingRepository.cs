using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  랭킹 조회
    ///
    ///  서버 콘솔에서 rank 를 치면 TOP 10 을 출력합니다.
    ///
    ///  SQL   SELECT nickname, best_score, games_played
    ///        FROM players
    ///        ORDER BY best_score DESC, id
    ///        LIMIT @n
    ///
    ///  예제 I 3번(여러 줄 읽기)과 같은 모양입니다.
    ///  best_score 에 인덱스가 있어서 플레이어가 많아도 10줄만 읽습니다. (7주차)
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

        public static async Task<List<RankRow>> TopAsync(int n)
        {
            // ═══════════════════════════════════════
            // TODO 8 (보너스) : 여러 줄 읽기 → List<RankRow>
            //
            //   while (await reader.ReadAsync()) { list.Add(...); }
            //   LIMIT 에도 파라미터(@n)를 쓸 수 있습니다.
            // ═══════════════════════════════════════
            // ── 여기에 작성 ──


            await Task.CompletedTask;
            return new List<RankRow>();   // ← 지우고 작성하세요
        }
    }
}
