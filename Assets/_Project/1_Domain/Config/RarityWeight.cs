namespace Card.Domain.Config
{
    /// <summary>稀有度权重（Docs/01 第 4.2 / 7.2 节 RarityWeights 表）。</summary>
    public sealed class RarityWeight
    {
        public CardRarity Rarity { get; init; } = CardRarity.Common;

        /// <summary>相对权重（如 70 / 22 / 6 / 2）。</summary>
        public int Weight { get; init; }

        /// <summary>单包至少出现几张（如稀及以上至少 1 张）。</summary>
        public int MinPerPack { get; init; }
    }
}
