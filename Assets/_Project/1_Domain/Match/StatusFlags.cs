using System;

namespace Card.Domain.Match
{
    /// <summary>
    /// 随从运行时状态位标记（M3-T3）：
    /// 召唤失调（进场当回合）、冻结（跳过下一次攻击）、圣盾（可被消耗一次）。
    /// 关键词是"能力来源"（配置），状态是"可结算实例"（运行时）。
    /// </summary>
    [Flags]
    public enum StatusFlags
    {
        None = 0,
        SummoningSickness = 1 << 0,
        Frozen = 1 << 1,
        DivineShield = 1 << 2
    }
}
