namespace Card.Domain.Config
{
    /// <summary>全局规则数值（Docs/01 第 7.2 节 Rules 表）：这些值必须来自配置，不得硬编码。</summary>
    public sealed class RulesConfig
    {
        public int HeroHealth { get; init; } = 30;

        public int HandLimit { get; init; } = 10;

        public int BoardLimit { get; init; } = 7;

        /// <summary>法力上限（首版 10）。</summary>
        public int ManaLimit { get; init; } = 10;

        /// <summary>卡组张数（严格等于，Docs/01 §7.2）。</summary>
        public int DeckSize { get; init; } = 30;

        /// <summary>先手起手牌数。</summary>
        public int StartingHandFirst { get; init; } = 3;

        /// <summary>后手起手牌数（另发幸运币）。</summary>
        public int StartingHandSecond { get; init; } = 4;

        /// <summary>幸运币卡牌 Key（指向 Cards 表的 0 费法术）。
        /// null = 配置未提供该列（旧配置/隔离构造），校验器跳过幸运币外键检查；
        /// 真实 Rules.csv 必须显式配置，MatchFactory 依赖它发放幸运币。</summary>
        public string? TheCoinCardKey { get; init; }
    }
}
