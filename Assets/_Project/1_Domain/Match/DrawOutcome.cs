using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>抽牌结算结果（M3-T8，纯数据）：供调用方（M4 状态机/事件系统）读取，不触发任何行为。</summary>
    public sealed class DrawOutcome
    {
        public DrawOutcome(
            IReadOnlyList<int> drawnInstanceIds,
            IReadOnlyList<int> burnedInstanceIds,
            int fatigueDamage,
            int finalFatigueCounter,
            bool heroDied)
        {
            DrawnInstanceIds = Guard.NotNull(drawnInstanceIds, nameof(drawnInstanceIds));
            BurnedInstanceIds = Guard.NotNull(burnedInstanceIds, nameof(burnedInstanceIds));
            FatigueDamage = fatigueDamage;
            FinalFatigueCounter = finalFatigueCounter;
            HeroDied = heroDied;
        }

        /// <summary>本次成功入手牌的实例 Id（按抽牌顺序）。</summary>
        public IReadOnlyList<int> DrawnInstanceIds { get; }

        /// <summary>手牌满被爆掉的实例 Id（按抽牌顺序）。</summary>
        public IReadOnlyList<int> BurnedInstanceIds { get; }

        /// <summary>本次疲劳伤害总量（多次空库抽牌之和）。</summary>
        public int FatigueDamage { get; }

        /// <summary>结算后的疲劳计数。</summary>
        public int FinalFatigueCounter { get; }

        /// <summary>结算后英雄生命 ≤ 0。</summary>
        public bool HeroDied { get; }
    }
}
