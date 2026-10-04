namespace Card.Domain.Config
{
    /// <summary>抽卡与保底配置（Docs/01 第 4 / 7.2 节）。</summary>
    public sealed class GachaConfig
    {
        /// <summary>每包张数（首版 5）。</summary>
        public int PackSize { get; init; } = 5;

        /// <summary>单包金币消耗。</summary>
        public int CoinCost { get; init; } = 100;

        /// <summary>保底包数：连续 N 包无保底稀有度则第 N+1 包必出。</summary>
        public int PityCount { get; init; } = 10;

        /// <summary>保底稀有度（默认传说）。</summary>
        public CardRarity PityRarity { get; init; } = CardRarity.Legendary;
    }
}
