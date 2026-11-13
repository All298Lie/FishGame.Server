using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

namespace BattleArenaServer
{
    /// <summary>판 결과 중 한 사람 몫 (match_results 한 줄)</summary>
    public class MatchEntry
    {
        public int    PlayerId { get; set; }   // ★ DB 의 players.id (접속 번호 PlayerId 가 아님)
        public string Nickname { get; set; } = "";
        public int    Score    { get; set; }
        public int    Ranking  { get; set; }
        public int    Coins    { get; set; }
        public int    Gems     { get; set; }
    }

    /// <summary>판 하나 (matches 한 줄 + match_results 여러 줄)</summary>
    public class MatchRecord
    {
        public int              RoundNo   { get; set; }
        public DateTime         StartedAt { get; set; }
        public DateTime         EndedAt   { get; set; }
        public List<MatchEntry> Entries   { get; set; } = new List<MatchEntry>();
    }

    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  판 결과 저장
    ///
    ///  한 판을 저장하려면 세 종류의 SQL 이 필요합니다.
    ///
    ///    ① INSERT INTO matches ...         새 판 번호를 받는다
    ///    ② INSERT INTO match_results ...   참가자 수만큼
    ///    ③ UPDATE players SET 통계 ...      참가자 수만큼
    ///
    ///  ②에서 실패하면? ①만 남은 "반쪽 판" 이 생기고,
    ///  ③에서 실패하면? 기록과 통계가 어긋납니다. (7주차 검산 쿼리에 걸림)
    ///
    ///  그래서 셋을 트랜잭션 하나로 묶습니다. 전부 되거나, 전부 안 되거나.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class MatchRepository
    {
        /// <summary>판 하나를 저장하고 새 판 번호(matches.id)를 돌려줍니다</summary>
        public static async Task<long> SaveAsync(MatchRecord match)
        {
            // ═══════════════════════════════════════
            // TODO 5 : 트랜잭션으로 판 저장
            //
            //   SQL                      C#
            //   ─────────────────────    ─────────────────────────────────────
            //   START TRANSACTION;       await using var tx = await conn.BeginTransactionAsync();
            //   (각 SQL)                 new MySqlCommand(sql, conn, tx)   ← tx 를 꼭 넘기세요
            //   COMMIT;                  await tx.CommitAsync();
            //   ROLLBACK;                await tx.RollbackAsync();   (catch 안에서)
            //
            //   ① INSERT INTO matches (started_at, ended_at) VALUES (@start, @end)
            //        → long matchId = cmd.LastInsertedId;
            //
            //   ② foreach (var e in match.Entries)
            //        INSERT INTO match_results (match_id, player_id, score, ranking, coins, gems)
            //        VALUES (@m, @p, @s, @r, @c, @g)
            //
            //   ③   UPDATE players SET
            //          total_score  = total_score + @s,
            //          best_score   = GREATEST(best_score, @s),
            //          games_played = games_played + 1
            //        WHERE id = @p
            //
            //   catch 에서 RollbackAsync 를 부른 뒤 throw; 로 에러를 다시 던지세요.
            //   (저장 큐가 "실패했다" 를 알아야 다시 시도할 수 있습니다)
            // ═══════════════════════════════════════
            // ── 여기에 작성 ──


            await Task.CompletedTask;
            throw new NotImplementedException("TODO 5 — 트랜잭션으로 판 저장");
        }
    }
}
