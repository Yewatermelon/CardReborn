using System.Collections.Generic;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T4：稀有度/抽卡/规则三类表的校验规则，以及报告文本格式。</summary>
    public sealed class ConfigValidatorTableTests
    {
        [Test]
        public void Validate_WhenRarityMissing_ReportsMissingRarity()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    weights: ConfigValidatorFixtures.Weights("Common,70,0\nRare,22,1\nEpic,6,0")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].TableName, Is.EqualTo(ConfigValidator.RarityWeightsTable));
            Assert.That(report.Issues[0].Message, Is.EqualTo("缺少稀有度配置：Legendary"));
            Assert.That(report.Issues[0].LineNumber, Is.EqualTo(0), "整表问题不带行号");
        }

        [Test]
        public void Validate_WhenWeightNotPositive_ReportsWeight()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    weights: ConfigValidatorFixtures.Weights("Common,0,0\nRare,22,1\nEpic,6,0\nLegendary,2,0")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Weight"));
            Assert.That(report.Issues[0].LineNumber, Is.EqualTo(2));
        }

        [Test]
        public void Validate_WhenRarityDuplicated_ReportsDuplicate()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    weights: ConfigValidatorFixtures.Weights(
                        "Common,70,0\nRare,22,1\nEpic,6,0\nEpic,2,0")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].Message, Does.Contain("稀有度重复"));
        }

        [Test]
        public void Validate_WhenMinPerPackNegative_ReportsMinPerPack()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    weights: ConfigValidatorFixtures.Weights(
                        "Common,70,-1\nRare,22,1\nEpic,6,0\nLegendary,2,0")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("MinPerPack"));
        }

        [Test]
        public void Validate_WhenGachaNumbersNotPositive_ReportsEachField()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(gacha: ConfigValidatorFixtures.Gacha("0,0,0,Legendary")));

            Assert.That(report.Count, Is.EqualTo(3), report.ToText());
            List<string> columns = new List<string>();
            for (int i = 0; i < report.Issues.Count; i++)
            {
                columns.Add(report.Issues[i].ColumnName);
            }

            Assert.That(columns, Is.EqualTo(new[] { "PackSize", "CoinCost", "PityCount" }));
        }

        [Test]
        public void Validate_WhenGachaHasTwoRows_ReportsSingleRowRule()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    gacha: ConfigValidatorFixtures.Gacha("5,100,10,Legendary\n6,120,12,Rare")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].Message, Does.Contain("必须是单行表"));
        }

        [Test]
        public void Validate_WhenRulesOutOfRange_ReportsEveryLimit()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(rules: ConfigValidatorFixtures.Rules("0,11,8,11")));

            Assert.That(report.Count, Is.EqualTo(4), report.ToText());
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("HeroHealth"));
            Assert.That(report.Issues[1].ColumnName, Is.EqualTo("HandLimit"));
            Assert.That(report.Issues[2].ColumnName, Is.EqualTo("BoardLimit"));
            Assert.That(report.Issues[3].ColumnName, Is.EqualTo("ManaLimit"));
        }

        [Test]
        public void Validate_WhenRulesHasTwoRows_ReportsSingleRowRule()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    rules: ConfigValidatorFixtures.Rules("30,10,7,10\n31,10,7,10")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].TableName, Is.EqualTo(ConfigValidator.RulesTable));
            Assert.That(report.Issues[0].Message, Does.Contain("单行表"));
        }

        [Test]
        public void Validate_WhenRulesTableMissing_ReportsMissingTable()
        {
            ConfigSourceSet sources = new ConfigSourceSet(
                ConfigValidatorFixtures.Cards(ConfigValidatorFixtures.ValidCardRow),
                ConfigValidatorFixtures.Heroes(ConfigValidatorFixtures.ValidHeroRow),
                ConfigValidatorFixtures.Powers(ConfigValidatorFixtures.ValidPowerRow),
                ConfigValidatorFixtures.Weights(ConfigValidatorFixtures.ValidWeightsBody),
                ConfigValidatorFixtures.Gacha("5,100,10,Legendary"),
                null);

            ConfigValidationReport report = ConfigValidator.Validate(sources);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].TableName, Is.EqualTo(ConfigValidator.RulesTable));
            Assert.That(report.Issues[0].Message, Is.EqualTo("源表缺失"));
        }

        [Test]
        public void Report_ToText_WhenNoErrors_SaysPassed()
        {
            ConfigValidationReport report = ConfigValidator.Validate(ConfigValidatorFixtures.Build());

            Assert.That(report.ToText(), Is.EqualTo("配置校验通过。"));
        }

        [Test]
        public void Report_ToText_WhenErrors_ListsCountAndEveryIssue()
        {
            string row = ConfigValidatorFixtures.ValidCardRow.Replace(",2,Minion,", ",abc,Minion,");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    cards: ConfigValidatorFixtures.Cards(row),
                    weights: ConfigValidatorFixtures.Weights("Common,70,0\nRare,22,1\nEpic,6,0")));

            string text = report.ToText();

            Assert.That(text, Does.StartWith("配置校验失败：共 2 个问题"));
            Assert.That(text, Does.Contain("- Cards.csv 第 2 行 [Cost]"));
            Assert.That(text, Does.Contain("- RarityWeights.csv [Rarity]：缺少稀有度配置：Legendary"));
        }

        [Test]
        public void Issue_ToString_WhenTableLevel_OmitsLineNumber()
        {
            ConfigValidationIssue issue = new ConfigValidationIssue("GachaConfig.csv", 0, string.Empty, "源表缺失");

            Assert.That(issue.ToString(), Is.EqualTo("GachaConfig.csv：源表缺失"));
        }

        [Test]
        public void Issue_ToString_WhenRowLevel_IncludesLineAndColumn()
        {
            ConfigValidationIssue issue = new ConfigValidationIssue("Cards.csv", 7, "Cost", "不能为负数，实际为 -1");

            Assert.That(issue.ToString(), Is.EqualTo("Cards.csv 第 7 行 [Cost]：不能为负数，实际为 -1"));
        }
    }
}
