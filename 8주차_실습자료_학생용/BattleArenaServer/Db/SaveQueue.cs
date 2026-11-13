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
            // TODO 6 : 저장 작업자
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
            // ── 여기에 작성 ──


            await Task.CompletedTask;   // ← 지우고 작성하세요
        }
    }
}
