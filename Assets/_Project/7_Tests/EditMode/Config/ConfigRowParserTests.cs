using Card.Core;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T3：CSV 行 → 配置契约（成功路径 + 可定位的失败路径）。</summary>
    public sealed class ConfigRowParserTests
    {
        private const string CardHeader =
            "Id,Key,NameKey,DescKey,Cost,Type,Rarity,Class,Attack,Health,Keywords,TargetRule,Effects,SetKey,ArtKey,AudioKey,Enabled";

        private const string ValidCardRow =
            "1,NEUTRAL_PANGO,CARD_001_NAME,CARD_001_DESC,2,Minion,Common,Neutral,2,3,Taunt|Charge,None,DamageEffect:2|GainArmorEffect:1,Core,art_card_001,sfx_play_001,TRUE";

        [Test]
        public void ParseCard_WhenValidRow_MapsAllFields()
        {
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + ValidCardRow + "\n");

            CardDefinition card = ConfigRowParser.ParseCard(table, 0, "Cards.csv");

            Assert.That(card.Id, Is.EqualTo(1));
            Assert.That(card.Key, Is.EqualTo("NEUTRAL_PANGO"));
            Assert.That(card.NameKey, Is.EqualTo("CARD_001_NAME"));
            Assert.That(card.DescKey, Is.EqualTo("CARD_001_DESC"));
            Assert.That(card.Cost, Is.EqualTo(2));
            Assert.That(card.Type, Is.EqualTo(CardType.Minion));
            Assert.That(card.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(card.Class, Is.EqualTo(CardClass.Neutral));
            Assert.That(card.Attack, Is.EqualTo(2));
            Assert.That(card.Health, Is.EqualTo(3));
            Assert.That(card.Keywords, Is.EqualTo(Keyword.Taunt | Keyword.Charge));
            Assert.That(card.TargetRule, Is.EqualTo(TargetRule.None));
            Assert.That(card.Effects, Is.EqualTo(new[] { "DamageEffect:2", "GainArmorEffect:1" }));
            Assert.That(card.SetKey, Is.EqualTo("Core"));
            Assert.That(card.ArtKey, Is.EqualTo("art_card_001"));
            Assert.That(card.AudioKey, Is.EqualTo("sfx_play_001"));
            Assert.That(card.Enabled, Is.True);
        }

        [Test]
        public void ParseCard_WhenKeywordsEmpty_YieldsNone()
        {
            string row = ValidCardRow.Replace("Taunt|Charge", "");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            CardDefinition card = ConfigRowParser.ParseCard(table, 0, "Cards.csv");

            Assert.That(card.Keywords, Is.EqualTo(Keyword.None));
        }

        [Test]
        public void ParseCard_WhenEffectsEmpty_YieldsEmptyList()
        {
            string row = ValidCardRow.Replace("DamageEffect:2|GainArmorEffect:1", "");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            CardDefinition card = ConfigRowParser.ParseCard(table, 0, "Cards.csv");

            Assert.That(card.Effects, Is.Empty);
        }

        [Test]
        public void ParseCard_WhenStatsEmpty_DefaultsToZero()
        {
            string row = ValidCardRow.Replace(",2,3,Taunt|Charge", ",,,Taunt|Charge");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            CardDefinition card = ConfigRowParser.ParseCard(table, 0, "Cards.csv");

            Assert.That(card.Attack, Is.EqualTo(0));
            Assert.That(card.Health, Is.EqualTo(0));
        }

        [Test]
        public void ParseCard_WhenCostIsNotInteger_ReportsTableLineAndColumn()
        {
            string row = ValidCardRow.Replace(",2,Minion,", ",abc,Minion,");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.TableName, Is.EqualTo("Cards.csv"));
            Assert.That(exception.LineNumber, Is.EqualTo(2));
            Assert.That(exception.ColumnName, Is.EqualTo("Cost"));
            Assert.That(exception.Message, Does.Contain("必须是整数"));
            Assert.That(exception.Message, Does.Contain("abc"));
        }

        [Test]
        public void ParseCard_WhenTypeIsNumeric_Rejects()
        {
            string row = ValidCardRow.Replace(",Minion,Common,", ",1,Common,");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.ColumnName, Is.EqualTo("Type"));
            Assert.That(exception.Message, Does.Contain("禁止写数字"));
        }

        [Test]
        public void ParseCard_WhenTypeUnknown_Rejects()
        {
            string row = ValidCardRow.Replace(",Minion,Common,", ",Dragon,Common,");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.ColumnName, Is.EqualTo("Type"));
        }

        [Test]
        public void ParseCard_WhenKeywordUnknown_ReportsKeywordsColumn()
        {
            string row = ValidCardRow.Replace("Taunt|Charge", "Taunt|Fly");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.ColumnName, Is.EqualTo("Keywords"));
            Assert.That(exception.Message, Does.Contain("Fly"));
        }

        [Test]
        public void ParseCard_WhenEnabledInvalid_ReportsEnabledColumn()
        {
            string row = ValidCardRow.Replace(",TRUE", ",yes");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.ColumnName, Is.EqualTo("Enabled"));
            Assert.That(exception.Message, Does.Contain("TRUE/FALSE/1/0"));
        }

        [Test]
        public void ParseCard_WhenRequiredColumnMissing_ReportsHeaderLine()
        {
            string header = CardHeader.Replace(",SetKey", "");
            string row = ValidCardRow.Replace(",Core,art_card_001", ",art_card_001");
            CsvTable table = CsvTable.Parse(header + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.ColumnName, Is.EqualTo("SetKey"));
            Assert.That(exception.LineNumber, Is.EqualTo(1), "缺列属于表头问题，定位到第 1 行");
            Assert.That(exception.Message, Does.Contain("表头缺少该列"));
        }

        [Test]
        public void ParseCard_WhenRequiredTextIsEmpty_Rejects()
        {
            string row = ValidCardRow.Replace("NEUTRAL_PANGO", "");
            CsvTable table = CsvTable.Parse(CardHeader + "\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.ColumnName, Is.EqualTo("Key"));
            Assert.That(exception.Message, Does.Contain("不能为空"));
        }

        [Test]
        public void ParseCard_WhenBlankLinesBeforeRow_ReportsRealSourceLine()
        {
            string row = ValidCardRow.Replace(",2,Minion,", ",abc,Minion,");
            CsvTable table = CsvTable.Parse(CardHeader + "\n\n" + row + "\n");

            ConfigFormatException exception = Assert.Throws<ConfigFormatException>(
                () => ConfigRowParser.ParseCard(table, 0, "Cards.csv"))!;

            Assert.That(exception.LineNumber, Is.EqualTo(3), "空行会让行号偏移，必须报告真实行号");
        }

        [Test]
        public void ParseHero_WhenValidRow_MapsFields()
        {
            CsvTable table = CsvTable.Parse(
                "Id,Key,NameKey,Health,HeroPowerKey,Class\n" +
                "1,HERO_MAGE,HERO_001_NAME,30,HERO_POWER_FIREBALL,Mage\n");

            HeroDefinition hero = ConfigRowParser.ParseHero(table, 0, "Heroes.csv");

            Assert.That(hero.Id, Is.EqualTo(1));
            Assert.That(hero.Key, Is.EqualTo("HERO_MAGE"));
            Assert.That(hero.Health, Is.EqualTo(30));
            Assert.That(hero.HeroPowerKey, Is.EqualTo("HERO_POWER_FIREBALL"));
            Assert.That(hero.Class, Is.EqualTo(CardClass.Mage));
        }

        [Test]
        public void ParseHeroPower_WhenValidRow_MapsFields()
        {
            CsvTable table = CsvTable.Parse(
                "Id,Key,Cost,TargetRule,Effects\n" +
                "1,HERO_POWER_FIREBALL,2,Any,DamageEffect:1\n");

            HeroPowerDefinition power = ConfigRowParser.ParseHeroPower(table, 0, "HeroPowers.csv");

            Assert.That(power.Key, Is.EqualTo("HERO_POWER_FIREBALL"));
            Assert.That(power.Cost, Is.EqualTo(2));
            Assert.That(power.TargetRule, Is.EqualTo(TargetRule.Any));
            Assert.That(power.Effects, Is.EqualTo(new[] { "DamageEffect:1" }));
        }

        [Test]
        public void ParseRarityWeight_WhenValidRow_MapsFields()
        {
            CsvTable table = CsvTable.Parse("Rarity,Weight,MinPerPack\nLegendary,2,0\n");

            RarityWeight weight = ConfigRowParser.ParseRarityWeight(table, 0, "RarityWeights.csv");

            Assert.That(weight.Rarity, Is.EqualTo(CardRarity.Legendary));
            Assert.That(weight.Weight, Is.EqualTo(2));
            Assert.That(weight.MinPerPack, Is.EqualTo(0));
        }

        [Test]
        public void ParseGachaConfig_WhenValidRow_MapsFields()
        {
            CsvTable table = CsvTable.Parse("PackSize,CoinCost,PityCount,PityRarity\n5,100,10,Legendary\n");

            GachaConfig gacha = ConfigRowParser.ParseGachaConfig(table, 0, "GachaConfig.csv");

            Assert.That(gacha.PackSize, Is.EqualTo(5));
            Assert.That(gacha.CoinCost, Is.EqualTo(100));
            Assert.That(gacha.PityCount, Is.EqualTo(10));
            Assert.That(gacha.PityRarity, Is.EqualTo(CardRarity.Legendary));
        }

        [Test]
        public void ParseRules_WhenValidRow_MapsFields()
        {
            CsvTable table = CsvTable.Parse("HeroHealth,HandLimit,BoardLimit,ManaLimit\n30,10,7,10\n");

            RulesConfig rules = ConfigRowParser.ParseRules(table, 0, "Rules.csv");

            Assert.That(rules.HeroHealth, Is.EqualTo(30));
            Assert.That(rules.HandLimit, Is.EqualTo(10));
            Assert.That(rules.BoardLimit, Is.EqualTo(7));
            Assert.That(rules.ManaLimit, Is.EqualTo(10));
        }
    }
}
