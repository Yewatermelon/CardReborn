using UnityEngine;

namespace Card.Bootstrap
{
    /// <summary>
    /// Unity 版逻辑时钟（M14-T3）：权威宿主按固定 10 Hz tick 推进（Docs/05 §4.3），
    /// 用未缩放时间换算 tick。规则层只经 <see cref="Card.Core.IClock"/> 取时，本类仅装配层引用。
    /// </summary>
    public sealed class UnityTickClock : Card.Core.IClock
    {
        /// <summary>权威 tick 频率（每秒 tick 数）。</summary>
        public const float TicksPerSecond = 10f;

        public int CurrentTick => (int)(Time.unscaledTimeAsDouble * TicksPerSecond);
    }
}
