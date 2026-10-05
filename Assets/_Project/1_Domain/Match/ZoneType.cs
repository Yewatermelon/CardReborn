namespace Card.Domain.Match
{
    /// <summary>卡牌分区类型（Docs/01 §3.2）：牌库 / 手牌 / 战场 / 坟场。</summary>
    public enum ZoneType
    {
        Deck = 0,
        Hand = 1,
        Board = 2,
        Graveyard = 3
    }
}
