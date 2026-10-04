namespace Card.Domain.Config
{
    /// <summary>英雄配置（Docs/01 第 7.2 节 Heroes 表）。</summary>
    public sealed class HeroDefinition
    {
        public int Id { get; init; }

        public string Key { get; init; } = string.Empty;

        public string NameKey { get; init; } = string.Empty;

        /// <summary>初始生命（默认 30，由 Rules 表约束）。</summary>
        public int Health { get; init; } = 30;

        /// <summary>英雄技能 Key（指向 HeroPowers 表）。</summary>
        public string HeroPowerKey { get; init; } = string.Empty;

        public CardClass Class { get; init; } = CardClass.Neutral;
    }
}
