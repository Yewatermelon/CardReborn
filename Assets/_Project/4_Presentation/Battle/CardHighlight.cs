using System;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 卡牌高亮旗标（M5-T6；FR-8.6）：可出牌/可攻击目标/嘲讽三态视觉区分。
    /// 位组合——一个嘲讽随从同时可以可攻击。
    /// </summary>
    [Flags]
    public enum CardHighlight
    {
        None = 0,
        Playable = 1 << 0,
        Attackable = 1 << 1,
        Taunt = 1 << 2
    }
}
