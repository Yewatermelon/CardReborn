namespace Card.Domain.Config
{
    /// <summary>
    /// 配置 schema 的版本与文件名契约（Docs/03 第 9.1 节：生成物必须带 schemaVersion，
    /// 与 CurrentVersion 不匹配时警告并尝试迁移）。
    /// 生成物由导入器写入 Assets/_Project/Config/，禁止手改。
    /// </summary>
    public static class ConfigSchema
    {
        /// <summary>当前 schema 版本；新增字段只允许追加（向后兼容）。</summary>
        public const int CurrentVersion = 1;

        /// <summary>生成物里承载版本号的字段名。</summary>
        public const string VersionProperty = "schemaVersion";

        public const string CardsFileName = "cards.json";
        public const string HeroesFileName = "heroes.json";
        public const string HeroPowersFileName = "hero_powers.json";
        public const string GachaFileName = "gacha.json";
        public const string RulesFileName = "rules.json";
    }
}
