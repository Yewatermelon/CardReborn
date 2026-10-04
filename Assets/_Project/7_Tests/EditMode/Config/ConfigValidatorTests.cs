using Card.Core;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T4：卡牌/英雄/技能三类表的校验规则。</summary>
    public sealed class ConfigValidatorTests
    {
        [Test]
        public void Validate_WhenAllTablesValid_HasNoErrorsAndReturnsBundle()
        {
            ConfigValidationReport report = ConfigValidator.Validate(ConfigValidatorFixtures.Build());

            Assert.That(report.HasErrors, Is.False, report.ToText());
            Assert.That(report.Count, Is.EqualTo(0));
            Assert.That(report.Result, Is.Not.Null);
            Assert.That(report.Result!.Cards.Count, Is.EqualTo(1));
            Assert.That(report.Result.Heroes.Count, Is.EqualTo(1));
            Assert.That(report.Result.HeroPowers.Count, Is.EqualTo(1));
            Assert.That(report.Result.RarityWeights.Count, Is.EqualTo(4));
        }

        [Test]
        public void Validate_WhenRealTemplatesUsed_HasNoErrors()
        {
            ConfigSourceSet sources = new ConfigSourceSet(
                ConfigTemplates.Load(ConfigTemplates.CardsFile),
                ConfigTemplates.Load(ConfigTemplates.HeroesFile),
                ConfigTemplates.Load(ConfigTemplates.HeroPowersFile),
                ConfigTemplates.Load(ConfigTemplates.RarityWeightsFile),
                ConfigTemplates.Load(ConfigTemplates.GachaConfigFile),
                ConfigTemplates.Load(ConfigTemplates.RulesFile));

            ConfigValidationReport report = ConfigValidator.Validate(sources);

            Assert.That(report.HasErrors, Is.False, report.ToText());
            Assert.That(report.Result!.Cards.Count, Is.EqualTo(sources.Cards!.RowCount));
        }

        [Test]
        public void Validate_WhenCardsTableMissing_ReportsMissingTableWithoutResult()
        {
            ConfigSourceSet sources = new ConfigSourceSet(
                null,
                ConfigValidatorFixtures.Heroes(ConfigValidatorFixtures.ValidHeroRow),
                ConfigValidatorFixtures.Powers(ConfigValidatorFixtures.ValidPowerRow),
                ConfigValidatorFixtures.Weights(ConfigValidatorFixtures.ValidWeightsBody),
                ConfigValidatorFixtures.Gacha("5,100,10,Legendary"),
                ConfigValidatorFixtures.Rules("30,10,7,10"));

            ConfigValidationReport report = ConfigValidator.Validate(sources);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Result, Is.Null, "有错就不产出生成物");
            Assert.That(report.Issues[0].TableName, Is.EqualTo(ConfigValidator.CardsTable));
            Assert.That(report.Issues[0].Message, Is.EqualTo("源表缺失"));
            Assert.That(report.Issues[0].LineNumber, Is.EqualTo(0));
        }

        [Test]
        public void Validate_WhenRowHasBadEnum_ReportsTableLineAndColumn()
        {
            string row = ConfigValidatorFixtures.ValidCardRow.Replace(",2,Minion,", ",2,Dragon,");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].TableName, Is.EqualTo(ConfigValidator.CardsTable));
            Assert.That(report.Issues[0].LineNumber, Is.EqualTo(2));
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Type"));
        }

        [Test]
        public void Validate_WhenMultipleProblemsAcrossTables_ListsAllOfThem()
        {
            string badCard = ConfigValidatorFixtures.ValidCardRow.Replace(",2,Minion,", ",abc,Minion,");
            string duplicateId = ConfigValidatorFixtures.ValidCardRow.Replace("NEUTRAL_PANGO", "NEUTRAL_OTHER");

            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    cards: ConfigValidatorFixtures.CardsWith(badCard, duplicateId),
                    weights: ConfigValidatorFixtures.Weights("Common,70,0\nRare,22,1\nEpic,6,0")));

            Assert.That(report.Count, Is.EqualTo(2), report.ToText());
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Cost"));
            Assert.That(report.Issues[1].Message, Does.Contain("缺少稀有度配置"));
        }

        [Test]
        public void Validate_WhenCardIdDuplicated_ReportsIdDuplicate()
        {
            string second = ConfigValidatorFixtures.ValidCardRow.Replace("NEUTRAL_PANGO", "NEUTRAL_OTHER");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    cards: ConfigValidatorFixtures.CardsWith(
                        ConfigValidatorFixtures.ValidCardRow,
                        second)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Id"));
            Assert.That(report.Issues[0].LineNumber, Is.EqualTo(3));
        }

        [Test]
        public void Validate_WhenCardKeyDuplicated_ReportsKeyDuplicate()
        {
            const string second =
                "2,NEUTRAL_PANGO,CARD_001_NAME,CARD_001_DESC,2,Minion,Common,Neutral,2,3,Taunt,None,,Core,art_card_002,sfx_play_002,TRUE";
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    cards: ConfigValidatorFixtures.CardsWith(
                        ConfigValidatorFixtures.ValidCardRow,
                        second)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Key"));
        }

        [Test]
        public void Validate_WhenHeroPowerKeyDangling_ReportsReference()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    heroes: ConfigValidatorFixtures.Heroes(
                        "1,HERO_MAGE,HERO_001_NAME,30,HERO_POWER_NOPE,Mage")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("HeroPowerKey"));
            Assert.That(report.Issues[0].Message, Does.Contain("HERO_POWER_NOPE"));
        }

        [Test]
        public void Validate_WhenPowersTableMissing_SkipsForeignKeyCheckButReportsMissingTable()
        {
            ConfigSourceSet sources = new ConfigSourceSet(
                ConfigValidatorFixtures.Cards(ConfigValidatorFixtures.ValidCardRow),
                ConfigValidatorFixtures.Heroes(ConfigValidatorFixtures.ValidHeroRow),
                null,
                ConfigValidatorFixtures.Weights(ConfigValidatorFixtures.ValidWeightsBody),
                ConfigValidatorFixtures.Gacha("5,100,10,Legendary"),
                ConfigValidatorFixtures.Rules("30,10,7,10"));

            ConfigValidationReport report = ConfigValidator.Validate(sources);

            Assert.That(report.Count, Is.EqualTo(1), "缺表只报一次，不应级联出'技能不存在'\n" + report.ToText());
            Assert.That(report.Issues[0].TableName, Is.EqualTo(ConfigValidator.HeroPowersTable));
        }

        [Test]
        public void Validate_WhenMinionHealthIsZero_ReportsHealth()
        {
            string row = ConfigValidatorFixtures.ValidCardRow.Replace(",2,3,Taunt", ",2,0,Taunt");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Health"));
            Assert.That(report.Issues[0].Message, Does.Contain("≥ 1"));
        }

        [Test]
        public void Validate_WhenSpellHasStats_ReportsNonMinionStats()
        {
            string row = ConfigValidatorFixtures.ValidCardRow
                .Replace(",Minion,Common,Neutral,2,3", ",Spell,Common,Neutral,2,3");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].Message, Does.Contain("非随从不应有攻击/生命"));
        }

        [Test]
        public void Validate_WhenCostIsNegative_ReportsCost()
        {
            string row = ConfigValidatorFixtures.ValidCardRow.Replace(",2,Minion,", ",-1,Minion,");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Cost"));
            Assert.That(report.Issues[0].Message, Does.Contain("不能为负数"));
        }

        [Test]
        public void Validate_WhenCostExceedsManaLimit_ReportsCost()
        {
            string row = ConfigValidatorFixtures.ValidCardRow.Replace(",2,Minion,", ",11,Minion,");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].Message, Does.Contain("ManaLimit=10"));
        }

        [Test]
        public void Validate_WhenCostEqualsManaLimit_Passes()
        {
            string row = ConfigValidatorFixtures.ValidCardRow.Replace(",2,Minion,", ",10,Minion,");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.False, report.ToText());
        }

        [Test]
        public void Validate_WhenCardDisabled_SkipsSemanticChecks()
        {
            string row = ConfigValidatorFixtures.ValidCardRow
                .Replace(",2,3,Taunt", ",2,0,Taunt")
                .Replace(",TRUE", ",FALSE");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.False, "废弃卡允许保留越界数据：" + report.ToText());
        }

        [Test]
        public void Validate_WhenDisabledCardIsUnparsable_StillReportsParseError()
        {
            string row = ConfigValidatorFixtures.ValidCardRow
                .Replace(",2,Minion,", ",2,Dragon,")
                .Replace(",TRUE", ",FALSE");
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(cards: ConfigValidatorFixtures.Cards(row)));

            Assert.That(report.HasErrors, Is.True, "禁用不等于可以不合法");
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Type"));
        }

        [Test]
        public void Validate_WhenHeroHealthOutOfRange_ReportsHealth()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    heroes: ConfigValidatorFixtures.Heroes(
                        "1,HERO_MAGE,HERO_001_NAME,0,HERO_POWER_FIREBALL,Mage")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Health"));
        }

        [Test]
        public void Validate_WhenHeroPowerCostNotPositive_ReportsCost()
        {
            ConfigValidationReport report = ConfigValidator.Validate(
                ConfigValidatorFixtures.Build(
                    powers: ConfigValidatorFixtures.Powers(
                        "1,HERO_POWER_FIREBALL,0,Any,DamageEffect:1")));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].ColumnName, Is.EqualTo("Cost"));
        }
    }
}
