namespace Card.Domain.Match
{
    /// <summary>
    /// 运行时状态集合（M3-T3）：召唤失调/冻结/圣盾的位标记增删查。
    /// 圣盾有"抵消一次伤害"语义，由 <see cref="ConsumeDivineShield"/> 原子检测并移除。
    /// </summary>
    public sealed class StatusSet
    {
        public StatusSet(StatusFlags flags = StatusFlags.None)
        {
            Flags = flags;
        }

        /// <summary>当前状态位组合。</summary>
        public StatusFlags Flags { get; private set; }

        public bool IsEmpty => Flags == StatusFlags.None;

        /// <summary>是否包含给定状态的全部位。</summary>
        public bool Has(StatusFlags flag)
        {
            return (Flags & flag) == flag;
        }

        /// <summary>加入一个或多个状态（幂等）。</summary>
        public void Add(StatusFlags flag)
        {
            Flags |= flag;
        }

        /// <summary>移除一个或多个状态（不含的位无副作用）。</summary>
        public void Remove(StatusFlags flag)
        {
            Flags &= ~flag;
        }

        /// <summary>按 <paramref name="enabled"/> 加入或移除状态。</summary>
        public void Toggle(StatusFlags flag, bool enabled)
        {
            if (enabled)
            {
                Add(flag);
            }
            else
            {
                Remove(flag);
            }
        }

        /// <summary>移除全部状态。</summary>
        public void Clear()
        {
            Flags = StatusFlags.None;
        }

        /// <summary>圣盾抵消一次伤害：有圣盾则移除并返回 true；无则 false（状态不变）。</summary>
        public bool ConsumeDivineShield()
        {
            if (!Has(StatusFlags.DivineShield))
            {
                return false;
            }

            Remove(StatusFlags.DivineShield);
            return true;
        }
    }
}
