using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  [9주차] 실시간 랭킹 — Redis Sorted Set
    ///
    ///  ── 키 하나에 전체 랭킹 ──
    ///
    ///    키      ba:rank:best          (게임:용도:종류 — 콜론으로 나누는 게 관례)
    ///    멤버    닉네임                 (이 게임은 닉네임 = 계정, 바뀌지 않음)
    ///    점수    최고 점수
    ///
    ///    Sorted Set 은 넣는 순간 점수 순으로 자리를 잡습니다.
    ///    그래서 TOP 10 도, "내가 몇 등인지" 도 바로 나옵니다.
    ///
    ///  ── 명령과 C# 메서드 ──
    ///
    ///    Redis 명령                              C#
    ///    ─────────────────────────────────────   ──────────────────────────────────────────
    ///    ZADD ba:rank:best 23 홍길동             SortedSetAddAsync(키, 닉, 점수)
    ///    ZREVRANGE ba:rank:best 0 9 WITHSCORES   SortedSetRangeByRankWithScoresAsync(키, 0, 9, Order.Descending)
    ///    ZREVRANK ba:rank:best 홍길동            SortedSetRankAsync(키, 닉, Order.Descending)
    ///    ZSCORE ba:rank:best 홍길동              SortedSetScoreAsync(키, 닉)
    ///    ZCARD ba:rank:best                      SortedSetLengthAsync(키)
    ///
    ///  ── 원본은 MySQL ──
    ///
    ///    Redis 를 껐다 켜서 비어 있으면 MySQL 의 players.best_score 로 다시 채웁니다(RebuildAsync).
    ///    그래서 Redis 에는 "잃어도 되는 것" 만 둡니다.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class RankingCache
    {
        public const string KEY = "ba:rank:best";

        public class Entry
        {
            public int    Rank     { get; set; }
            public string Nickname { get; set; } = "";
            public int    Score    { get; set; }
        }

        /// <summary>
        /// 최고 점수 갱신 — 판이 저장된 뒤 참가자마다 부릅니다
        ///
        /// 돌려주는 값 : 최고 기록이 바뀌었으면 true
        ///
        /// ★ 그냥 ZADD 를 하면 덮어씁니다
        ///   23점이던 사람이 이번 판 15점이면 15점이 됩니다. 그래서 "더 클 때만" 넣습니다.
        ///
        /// ★ 읽고 → 비교하고 → 쓰기 (세 단계)
        ///   ZSCORE 로 지금 점수를 읽고, C# 에서 비교하고, 클 때만 ZADD.
        ///   예제 R 3번에서 본 것처럼 세 단계 사이에 다른 요청이 끼어들 수 있는데,
        ///   우리 서버는 저장 작업자 "하나" 가 판을 차례로 처리하므로 끼어들 상대가 없습니다.
        ///   (서버가 여러 대가 되면 문제가 됩니다 → 최신 Redis 6.2+ 는 ZADD GT 한 명령으로 해결)
        /// </summary>
        public static async Task<bool> UpdateBestAsync(string nickname, int score)
        {
            // ═══════════════════════════════════════
            // TODO 2 : 최고 점수 갱신
            //
            //   Redis   ZSCORE ba:rank:best 닉네임          → 지금 점수 (없으면 nil)
            //           (더 크면) ZADD ba:rank:best 점수 닉네임
            //
            //   C#      double? now = await RedisCache.Db0.SortedSetScoreAsync(KEY, nickname);
            //           if (now == null || score > now.Value)
            //           {
            //               await RedisCache.Db0.SortedSetAddAsync(KEY, nickname, score);
            //               return true;
            //           }
            //           return false;
            //
            //   now == null : 랭킹에 처음 들어오는 사람 → 점수가 0 이어도 넣습니다
            // ═══════════════════════════════════════
            await Task.CompletedTask;
            throw new NotImplementedException("TODO 2");
        }

        /// <summary>TOP N — 점수 높은 순</summary>
        public static async Task<List<Entry>> TopAsync(int n)
        {
            // ═══════════════════════════════════════
            // TODO 3 : TOP N
            //
            //   Redis   ZREVRANGE ba:rank:best 0 (n-1) WITHSCORES
            //   C#      SortedSetRangeByRankWithScoresAsync(KEY, 0, n - 1, Order.Descending)
            //
            //   돌려받는 것 : SortedSetEntry[]   — e.Element (멤버, RedisValue) · e.Score (double)
            //   8주차 RankingRepository.TopAsync 처럼 List 로 옮겨 담습니다.
            //     Nickname = e.Element.ToString()
            //     Score    = (int)e.Score
            //     Rank     = i + 1        ← 배열 순서가 곧 순위
            // ═══════════════════════════════════════
            await Task.CompletedTask;
            throw new NotImplementedException("TODO 3");
        }

        /// <summary>
        /// 내 순위 (1위부터). 랭킹에 없으면 null
        ///
        /// ★ ZREVRANK 는 0부터 셉니다. 1등이 0 입니다. 그래서 + 1.
        /// ★ 없는 멤버면 Redis 가 nil 을 주고, C# 에서는 null 이 옵니다. (8주차 NULL 과 같은 이야기)
        /// </summary>
        public static async Task<int?> RankOfAsync(string nickname)
        {
            // ═══════════════════════════════════════
            // TODO 4 : 내 순위
            //
            //   Redis   ZREVRANK ba:rank:best 닉네임
            //   C#      long? r = await SortedSetRankAsync(KEY, nickname, Order.Descending);
            //
            //   r 이 null 이면 null, 아니면 (int)r + 1
            // ═══════════════════════════════════════
            await Task.CompletedTask;
            throw new NotImplementedException("TODO 4");
        }

        /// <summary>랭킹에 몇 명 있나</summary>
        public static async Task<long> CountAsync()
            => await RedisCache.Db0.SortedSetLengthAsync(KEY);

        /// <summary>
        /// MySQL 에서 다시 채우기 — Redis 가 비어 있을 때 (처음 켰을 때, 재시작했을 때)
        ///
        /// 돌려주는 값 : 채운 사람 수
        /// </summary>
        public static async Task<int> RebuildAsync()
        {
            // ── MySQL 에서 읽기 (8주차와 같은 모양, 완성본) ──
            List<RankingRepository.ScoreRow> rows = await RankingRepository.AllBestScoresAsync();

            // ═══════════════════════════════════════
            // TODO 5 : Redis 다시 채우기
            //
            //   1. 키를 지운다                    KeyDeleteAsync(KEY)
            //   2. 1000명씩 묶어서 넣는다         SortedSetAddAsync(KEY, SortedSetEntry[])
            //        new SortedSetEntry(닉네임, 점수)
            //
            //   ★ 한 명씩 10만 번 보내면 왕복이 10만 번입니다.
            //     묶어서 보내면 ZADD 한 번에 1000명 — 왕복이 100번으로 줄어듭니다.
            // ═══════════════════════════════════════
            throw new NotImplementedException("TODO 5");
        }

        /// <summary>
        /// 서버 시작 · 재연결 때 — 비어 있으면 다시 채웁니다 (완성본)
        /// </summary>
        public static async Task EnsureFilledAsync(string why)
        {
            if (!RedisCache.IsAvailable || !Db.IsAvailable) return;

            try
            {
                if (await RedisCache.Db0.KeyExistsAsync(KEY))
                {
                    Console.WriteLine($"[랭킹] Redis 에 {await CountAsync()}명 있음 ({why})");
                    return;
                }

                var sw = Stopwatch.StartNew();
                int n = await RebuildAsync();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[랭킹] Redis 가 비어 있어 MySQL 에서 {n}명을 채웠습니다 ({sw.ElapsedMilliseconds}ms · {why})");
                Console.ResetColor();
            }
            catch (NotImplementedException ex)
            {
                Console.WriteLine($"[랭킹] {ex.Message} 가 비어 있습니다 — Redis 를 채우지 않았습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[랭킹] 다시 채우기 실패 : {ex.Message}");
            }
        }
    }
}
