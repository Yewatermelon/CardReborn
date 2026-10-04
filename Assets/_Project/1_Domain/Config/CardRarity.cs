namespace Card.Domain.Config
{
    /// <summary>稀有度（Docs/01 第 4.1 节）：决定抽卡权重与合成/分解价格。</summary>
    public enum CardRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }
}
