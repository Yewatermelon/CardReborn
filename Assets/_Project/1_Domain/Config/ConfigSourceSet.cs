using Card.Core;
using System.Collections.Generic;

namespace Card.Domain.Config
{
    /// <summary>一次导入涉及的源表集合。缺表用 null 表示——缺失本身就是校验错误（不是崩溃理由）。</summary>
    public sealed class ConfigSourceSet
    {
        public ConfigSourceSet(
            CsvTable? cards,
            CsvTable? heroes,
            CsvTable? heroPowers,
            CsvTable? rarityWeights,
            CsvTable? gachaConfig,
            CsvTable? rules,
            IReadOnlyList<ConfigValidationIssue>? loadingIssues = null)
        {
            Cards = cards;
            Heroes = heroes;
            HeroPowers = heroPowers;
            RarityWeights = rarityWeights;
            GachaConfig = gachaConfig;
            Rules = rules;
            LoadingIssues = loadingIssues ?? System.Array.Empty<ConfigValidationIssue>();
        }

        public CsvTable? Cards { get; }

        public CsvTable? Heroes { get; }

        public CsvTable? HeroPowers { get; }

        public CsvTable? RarityWeights { get; }

        public CsvTable? GachaConfig { get; }

        public CsvTable? Rules { get; }

        /// <summary>读取阶段的既有问题（如 CSV 结构错误）；校验器会一并报出。</summary>
        public IReadOnlyList<ConfigValidationIssue> LoadingIssues { get; }
    }
}
