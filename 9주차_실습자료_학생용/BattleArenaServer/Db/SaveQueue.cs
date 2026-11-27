using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    ///  저장 큐 — 게임 루프와 DB 를 떼어 놓는 곳
    ///
    ///  ── 왜 GameLoop 에서 바로 저장하면 안 되나 ──
    ///
    ///    틱 하나는 33ms 입니다.
    ///    DB 저장은 빠르면 몇 ms, 느리면 수백 ms, DB 가 죽어 있으면 몇 초입니다.
    ///
    ///      틱 안에서 await MatchRepository.SaveAsync(...)
    ///        → 저장이 끝날 때까지 다음 틱이 안 돕니다
    ///        → 그동안 전원이 멈춥니다 (이동 · 아이템 · STATE 전부)
    ///
    ///  ── 그래서 이렇게 나눕니다 ──
    ///
    ///      GameLoop      판이 끝나면 결과를 큐에 "넣기만" 함   ← 1ms 도 안 걸림
    ///      SaveQueue     따로 도는 작업자가 하나씩 꺼내 저장
    ///
    ///    6주차에 UdpServer(받기)와 GameLoop(판정)를 떼어 놓은 것과 같은 모양입니다.
    ///
    ///  ── Channel ──
    ///
    ///    ConcurrentQueue 와 비슷한데, "꺼낼 게 생길 때까지 기다리기" 를
    ///    await 로 할 수 있습니다. 비어 있으면 작업자는 CPU 를 안 쓰고 잠듭니다.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class SaveQueue
    {
        /// <summary>실패하면 몇 번까지 다시 시도할지</summary>
        private const int MAX_TRY = 3;

        /// <summary>다시 시도하기 전에 기다리는 시간</summary>
        private const int RETRY_DELAY_MS = 2000;

        private readonly Channel<MatchRecord> _channel = Channel.CreateUnbounded<MatchRecord>();

        private int _pending;
        private long _saved;
        private long _failed;

        public int  Pending => Volatile.Read(ref _pending);
        public long Saved   => Interlocked.Read(ref _saved);
        public long Failed  => Interlocked.Read(ref _failed);

        /// <summary>
        /// 저장 요청 넣기 — GameLoop 가 부릅니다
        ///
        /// ★ await 가 없습니다. 넣고 바로 돌아갑니다.
        /// </summary>
        public void Enqueue(MatchRecord match)
        {
            Interlocked.Increment(ref _pending);
            _channel.Writer.TryWrite(match);
        }

        /// <summary>작업자 — 서버 시작 때 한 번 띄워 두면 계속 돕니다</summary>
        public async Task RunAsync()
        {
            // ═══════════════════════════════════════
            // [8주차 TODO 6 · 완성] 저장 작업자
            //
            //   await foreach (MatchRecord m in _channel.Reader.ReadAllAsync())
            //   {
            //       1. MatchRepository.SaveAsync(m) 를 부른다
            //       2. 성공하면 "[저장] 3판 → matches.id 42 (2명)" 출력, _saved 증가
            //       3. 실패하면(catch) 2초 쉬고 다시. MAX_TRY 번까지
            //       4. 끝내 실패하면 "[저장 실패]" 출력, _failed 증가
            //       5. 어느 쪽이든 _pending 감소
            //   }
            //
            //   ★ 반드시 try/catch 로 감싸세요.
            //     예외가 밖으로 새면 작업자가 조용히 죽고,
            //     서버는 멀쩡해 보이는데 그 뒤로는 아무것도 저장되지 않습니다.
            // ═══════════════════════════════════════
            await foreach (MatchRecord m in _channel.Reader.ReadAllAsync())
            {
                bool ok = false;

                for (int attempt = 1; attempt <= MAX_TRY && !ok; attempt++)
                {
                    try
                    {
                        long matchId = await MatchRepository.SaveAsync(m);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[저장] {m.RoundNo}판 → matches.id {matchId} ({m.Entries.Count}명)");
                        Console.ResetColor();

                        Interlocked.Increment(ref _saved);
                        ok = true;

                        // ── 9주차 : MySQL 에 저장된 "뒤에" Redis 랭킹 갱신 ──
                        await UpdateRankingAsync(m);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[저장] {m.RoundNo}판 실패 ({attempt}/{MAX_TRY}) : {ex.Message}");
                        Console.ResetColor();

                        if (attempt < MAX_TRY)
                            await Task.Delay(RETRY_DELAY_MS);
                    }
                }

                if (!ok)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[저장 실패] {m.RoundNo}판을 끝내 저장하지 못했습니다.");
                    Console.ResetColor();
                    Interlocked.Increment(ref _failed);
                }

                Interlocked.Decrement(ref _pending);
            }
        }

        /// <summary>
        /// [9주차] 판이 MySQL 에 저장된 뒤 — Redis 랭킹을 갱신하고, 본인에게 순위를 알려 줍니다
        ///
        /// ★ 순서가 중요합니다 : MySQL(원본) 먼저, Redis(사본) 나중
        ///
        ///   Redis 먼저 갱신했는데 MySQL 저장이 실패하면?
        ///     → 랭킹에는 있는데 기록은 없는 점수가 생깁니다. 원본에 없는 사본.
        ///   MySQL 먼저 저장했는데 Redis 갱신이 실패하면?
        ///     → 랭킹이 잠깐 늦을 뿐, 다음에 다시 채우면 맞춰집니다.
        ///
        /// ★ Redis 가 실패해도 "저장 실패" 가 아닙니다
        ///
        ///   이 함수 안에서 예외를 다 잡습니다. 밖으로 새면 위의 재시도 루프의 catch 가 받아서
        ///   "저장 실패" 로 착각합니다.
        ///     · 지금 순서(ok = true 뒤에 호출) : 판은 저장됐는데 "[저장] N판 실패" 라는 틀린 로그
        ///     · 이 호출을 ok = true 앞으로 옮기면 : 같은 판을 MySQL 에 세 번 저장 (실험으로 확인)
        /// </summary>
        private static async Task UpdateRankingAsync(MatchRecord m)
        {
            if (!RedisCache.IsAvailable) return;

            try
            {
                // ═══════════════════════════════════════
                // TODO 6 : 판 결과로 Redis 랭킹 갱신
                //
                //   참가자마다 (e.PlayerId == 0 인 손님은 건너뜀 — DB 에 없는 사람)
                //     1. bool newBest = RankingCache.UpdateBestAsync(e.Nickname, e.Score)
                //     2. int? rank = RankingCache.RankOfAsync(e.Nickname)
                //     3. 접속 중이면 본인에게 알려 주기
                //          GameServer.Instance.FindSessionByDbId(e.PlayerId)
                //          ChatEchoPacket { Nickname = "랭킹", Message = "전체 3위 (최고 40점 · 새 기록!)" }
                //   마지막에 서버 로그 : "[랭킹] 3판 반영 — 홍길동 2위 · 김철수 5위"
                // ═══════════════════════════════════════
                await Task.CompletedTask;
                throw new NotImplementedException("TODO 6");
            }
            catch (NotImplementedException ex)
            {
                Console.WriteLine($"[랭킹] {ex.Message} 가 비어 있습니다 — 랭킹은 갱신하지 않았습니다.");
            }
            catch (Exception ex)
            {
                // Redis 가 죽어도 판은 MySQL 에 이미 저장됐습니다. 랭킹만 늦습니다.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[랭킹] Redis 갱신 실패 (판은 저장됨) : {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
