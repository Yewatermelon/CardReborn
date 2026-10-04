using System;

namespace Card.Domain.Config
{
    /// <summary>
    /// 关键词位标记（Docs/01 第 3.5 节，共 12 个）：一张卡可同时拥有多个，故用 flags 枚举。
    /// 配置表里写成 Taunt|Charge（见 KeywordTokens）。
    /// </summary>
    [Flags]
    public enum Keyword
    {
        None = 0,
        Taunt = 1 << 0,
        Charge = 1 << 1,
        DivineShield = 1 << 2,
        Battlecry = 1 << 3,
        Deathrattle = 1 << 4,
        Windfury = 1 << 5,
        Stealth = 1 << 6,
        Poisonous = 1 << 7,
        Frozen = 1 << 8,
        Rush = 1 << 9,
        SpellPower = 1 << 10,
        Lifesteal = 1 << 11
    }
}
