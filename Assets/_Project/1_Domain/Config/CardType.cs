namespace Card.Domain.Config
{
    /// <summary>卡牌类型（Docs/01 第 4.1 节）。Weapon / Hero 为预留值，首版不产卡。</summary>
    public enum CardType
    {
        Minion = 0,
        Spell = 1,
        Weapon = 2,
        Hero = 3
    }
}
