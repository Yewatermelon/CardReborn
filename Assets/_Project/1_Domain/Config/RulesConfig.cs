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
    }
}
