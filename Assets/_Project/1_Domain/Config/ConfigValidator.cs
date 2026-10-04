using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 配置校验器（FR-1.2）：**一次列出全部问题**，只要有一条就不产出生成物。
    /// 四层规则：表存在 → 行可解析 → 表内唯一 → 语义与跨表一致。
    /// 一行内若有多个非法字段，只报**首个**解析错误（行级定位已满足 FR-1.2；逐格累积登记为 P3）。
    /// 各表规则拆在 CardConfigValidator / HeroConfigValidator / RuleConfigValidator，本类只做编排与共享原语。
    /// </summary>
    public static class ConfigValidator
    {
        public const string CardsTable = "Cards.csv";
        public const string HeroesTable = "Heroes.csv";
        public const string HeroPowersTable = "HeroPowers.csv";
        public const string RarityWeightsTable = "RarityWeights.csv";
        public const string GachaConfigTable = "GachaConfig.csv";
        public const string RulesTable = "Rules.csv";

        /// <summary>校验全部源表；无问题时 Result 给出已解析契约集合。</summary>
        public static ConfigValidationReport Validate(ConfigSourceSet sources)
        {
            Guard.NotNull(sources, nameof(sources));

            List<ConfigValidationIssue> issues = new List<ConfigValidationIssue>();

            // 顺序：规则表先（卡片费用上界要用 ManaLimit）；技能表先于英雄表（英雄外键指向技能）。
            RulesConfig? rules = RuleConfigValidator.ValidateRules(sources.Rules, issues);
            List<HeroPowerDefinition> powers = HeroConfigValidator.ValidateHeroPowers(sources.HeroPowers, issues);
            List<HeroDefinition> heroes = HeroConfigValidator.ValidateHeroes(
                sources.Heroes,
                powers,
                sources.HeroPowers != null,
                issues);
            List<CardDefinition> cards = CardConfigValidator.ValidateCards(sources.Cards, rules, issues);
            List<RarityWeight> weights = RuleConfigValidator.ValidateRarityWeights(sources.RarityWeights, issues);
            GachaConfig? gacha = RuleConfigValidator.ValidateGacha(sources.GachaConfig, issues);

            ConfigBundle? bundle = null;
            if (issues.Count == 0)
            {
                bundle = new ConfigBundle(cards, heroes, powers, weights, gacha!, rules!);
            }

            return new ConfigValidationReport(issues, bundle);
        }

        internal static bool RequireTable(CsvTable? table, string tableName, List<ConfigValidationIssue> issues)
        {
            if (table != null)
            {
                return true;
            }

            Add(issues, tableName, 0, string.Empty, "源表缺失");
            return false;
        }

        internal static bool RequireSingleRow(CsvTable table, string tableName, List<ConfigValidationIssue> issues)
        {
            if (table.RowCount == 1)
            {
                return true;
            }

            Add(issues, tableName, 0, string.Empty, "必须是单行表，实际 " + table.RowCount + " 行");
            return false;
        }

        internal static void CheckUnique(
            List<ConfigValidationIssue> issues,
            string tableName,
            int line,
            HashSet<int> ids,
            HashSet<string> keys,
            int id,
            string key)
        {
            if (!ids.Add(id))
            {
                Add(issues, tableName, line, "Id", "Id 重复：" + id);
            }

            if (!keys.Add(key))
            {
                Add(issues, tableName, line, "Key", "Key 重复：" + key);
            }
        }

        internal static void Add(
            List<ConfigValidationIssue> issues,
            string tableName,
            int line,
            string column,
            string message)
        {
            issues.Add(new ConfigValidationIssue(tableName, line, column, message));
        }
    }
}
