using System;

namespace Card.Core
{
    /// <summary>
    /// 手动推进的逻辑时钟：测试与权威宿主（服务端 tick 循环）都可以驱动它。
    ///
    /// 约定：
    /// 1. 生产推进一律用 Advance（宿主每 tick 调一次 Advance(1)）；
    /// 2. SetTo 仅用于测试、重放与快照恢复——它允许回拨，滥用会掩盖时间逻辑缺陷；
    /// 3. tick 不允许为负；推进导致 int 溢出时抛异常，而不是静默回绕成负数；
    /// 4. 非线程安全（单线程 tick / 主线程约定）。
    /// </summary>
    public sealed class ManualClock : IClock
    {
        /// <summary>创建时钟；初始 tick 默认为 0。</summary>
        public ManualClock(int initialTick = 0)
        {
            if (initialTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialTick), initialTick, "tick 不能为负数。");
            }

            CurrentTick = initialTick;
        }

        /// <inheritdoc />
        public int CurrentTick { get; private set; }

        /// <summary>推进指定 tick 数（必须非负）；推进量过大导致溢出时抛 InvalidOperationException。</summary>
        public void Advance(int ticks)
        {
            if (ticks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ticks),
                    ticks,
                    "推进量不能为负数；回拨请用 SetTo（仅测试/重放）。");
            }

            if (ticks == 0)
            {
                return;
            }

            if (CurrentTick > int.MaxValue - ticks)
            {
                throw new InvalidOperationException(
                    "逻辑时钟溢出：" + CurrentTick + " + " + ticks);
            }

            CurrentTick += ticks;
        }

        /// <summary>直接设置逻辑时刻（仅测试 / 重放 / 恢复使用）；不允许负值。</summary>
        public void SetTo(int tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不能为负数。");
            }

            CurrentTick = tick;
        }
    }
}
