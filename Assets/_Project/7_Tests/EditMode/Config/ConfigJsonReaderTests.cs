using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T5：生成物 JSON → 契约（读取方向的单元测试，不依赖文件系统）。</summary>
    public sealed class ConfigJsonReaderTests
    {
        private const string CardJson =
            "{\"schemaVersion\":1,\"cards\":[{" +
            "\"id\":3,\"key\":\"MAGE_FIREBALL\",\"nameKey\":\"CARD_005_NAME\",\"descKey\":\"CARD_005_DESC\"," +
            "\"cost\":4,\"type\":\"Spell\",\"rarity\":\"Common\",\"class\":\"Mage\",\"attack\":0,\"health\":0," +
            "\"keywords\":\"\",\"targetRule\":\"EnemyMinion\",\"effects\":[\"DamageEffect:6\"]," +
            "\"setKey\":\"Core\",\"artKey\":\"art_spell_001\",\"audioKey\":\"sfx_cast_001\",\"enabled\":true}]}";

        [Test]
        public void ReadCards_WhenValid_MapsEveryField()
        {
            CardDefinition card = ConfigJsonReader.ReadCards(JsonValue.Parse(CardJson), "cards.json")[0];

            Assert.That(card.Id, Is.EqualTo(3));
            Assert.That(card.Key, Is.EqualTo("MAGE_FIREBALL"));
            Assert.That(card.NameKey, Is.EqualTo("CARD_005_NAME"));
            Assert.That(card.Cost, Is.EqualTo(4));
            Assert.That(card.Type, Is.EqualTo(CardType.Spell));
            Assert.That(card.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(card.Class, Is.EqualTo(CardClass.Mage));
            Assert.That(card.TargetRule, Is.EqualTo(TargetRule.EnemyMinion));
            Assert.That(card.Effects, Is.EqualTo(new[] { "DamageEffect:6" }));
            Assert.That(card.Keywords, Is.EqualTo(Keyword.None));
            Assert.That(card.SetKey, Is.EqualTo("Core"));
            Assert.That(card.Enabled, Is.True);
        }

        [Test]
        public void ReadCards_WhenKeywordsPresent_ParsesFlags()
        {
            string json = CardJson.Replace("\"keywords\":\"\"", "\"keywords\":\"Taunt|Charge\"");

            CardDefinition card = ConfigJsonReader.ReadCards(JsonValue.Parse(json), "cards.json")[0];

            Assert.That(card.Keywords, Is.EqualTo(Keyword.Taunt | Keyword.Charge));
        }

        [Test]
        public void ReadCards_WhenEmptyArray_ReturnsEmptyList()
        {
            IReadOnlyList<CardDefinition> cards = ConfigJsonReader.ReadCards(
                JsonValue.Parse("{\"schemaVersion\":1,\"cards\":[]}"),
                "cards.json");

            Assert.That(cards, Is.Empty);
        }

        [Test]
        public void ReadCards_WhenSchemaVersionDiffers_Throws()
        {
            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse("{\"schemaVersion\":99,\"cards\":[]}"), "cards.json"))!;

            Assert.That(exception.SourceName, Is.EqualTo("cards.json"));
            Assert.That(exception.Reason, Does.Contain("版本不匹配"));
        }

        [Test]
        public void ReadCards_WhenSchemaVersionMissing_Throws()
        {
            Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse("{\"cards\":[]}"), "cards.json"));
        }

        [Test]
        public void ReadCards_WhenRootIsNotObject_Throws()
        {
            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse("[]"), "cards.json"))!;

            Assert.That(exception.JsonPath, Is.EqualTo("<root>"));
        }

        [Test]
        public void ReadCards_WhenCardsIsNotArray_Throws()
        {
            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse("{\"schemaVersion\":1,\"cards\":1}"), "cards.json"))!;

            Assert.That(exception.Reason, Does.Contain("应为数组"));
        }

        [Test]
        public void ReadCards_WhenFieldMissing_ReportsJsonPath()
        {
            string json = CardJson.Replace("\"cost\":4,", string.Empty);

            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse(json), "cards.json"))!;

            Assert.That(exception.JsonPath, Is.EqualTo("cards[0]")); 
            Assert.That(exception.Reason, Does.Contain("cost"));
        }

        [Test]
        public void ReadCards_WhenFieldHasWrongType_ReportsType()
        {
            string json = CardJson.Replace("\"cost\":4", "\"cost\":\"4\"");

            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse(json), "cards.json"))!;

            Assert.That(exception.JsonPath, Does.Contain("cost"));
            Assert.That(exception.Reason, Does.Contain("应为整数"));
        }

        [Test]
        public void ReadCards_WhenEnumIsUnknown_Throws()
        {
            string json = CardJson.Replace("\"type\":\"Spell\"", "\"type\":\"Dragon\"");

            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse(json), "cards.json"))!;

            Assert.That(exception.Reason, Does.Contain("Dragon"));
        }

        [Test]
        public void ReadCards_WhenKeywordIsUnknown_Throws()
        {
            string json = CardJson.Replace("\"keywords\":\"\"", "\"keywords\":\"Fly\"");

            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse(json), "cards.json"))!;

            Assert.That(exception.Reason, Does.Contain("Fly"));
        }

        [Test]
        public void ReadCards_WhenEffectItemIsNotString_Throws()
        {
            string json = CardJson.Replace("[\"DamageEffect:6\"]", "[1]");

            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadCards(JsonValue.Parse(json), "cards.json"))!;

            Assert.That(exception.JsonPath, Does.Contain("effects[0]"));
        }

        [Test]
        public void ReadHeroesAndPowers_WhenValid_MapsFields()
        {
            IReadOnlyList<HeroDefinition> heroes = ConfigJsonReader.ReadHeroes(
                JsonValue.Parse("{\"schemaVersion\":1,\"heroes\":[{\"id\":1,\"key\":\"HERO_MAGE\"," +
                                "\"nameKey\":\"HERO_001_NAME\",\"health\":30,\"heroPowerKey\":\"HERO_POWER_FIREBALL\",\"class\":\"Mage\"}]}"),
                "heroes.json");
            IReadOnlyList<HeroPowerDefinition> powers = ConfigJsonReader.ReadHeroPowers(
                JsonValue.Parse("{\"schemaVersion\":1,\"heroPowers\":[{\"id\":1,\"key\":\"HERO_POWER_FIREBALL\"," +
                                "\"cost\":2,\"targetRule\":\"Any\",\"effects\":[\"DamageEffect:1\"]}]}"),
                "hero_powers.json");

            Assert.That(heroes[0].HeroPowerKey, Is.EqualTo("HERO_POWER_FIREBALL"));
            Assert.That(powers[0].Cost, Is.EqualTo(2));
            Assert.That(powers[0].TargetRule, Is.EqualTo(TargetRule.Any));
        }

        [Test]
        public void ReadRarityWeightsGachaAndRules_WhenValid_MapFields()
        {
            IReadOnlyList<RarityWeight> weights = ConfigJsonReader.ReadRarityWeights(
                JsonValue.Parse("{\"schemaVersion\":1,\"rarityWeights\":[{\"rarity\":\"Legendary\",\"weight\":2,\"minPerPack\":0}]}"),
                "rarity_weights.json");
            GachaConfig gacha = ConfigJsonReader.ReadGacha(
                JsonValue.Parse("{\"schemaVersion\":1,\"gacha\":{\"packSize\":5,\"coinCost\":100,\"pityCount\":10,\"pityRarity\":\"Legendary\"}}"),
                "gacha.json");
            RulesConfig rules = ConfigJsonReader.ReadRules(
                JsonValue.Parse("{\"schemaVersion\":1,\"rules\":{\"heroHealth\":30,\"handLimit\":10,\"boardLimit\":7,\"manaLimit\":10}}"),
                "rules.json");

            Assert.That(weights[0].Weight, Is.EqualTo(2));
            Assert.That(gacha.PityRarity, Is.EqualTo(CardRarity.Legendary));
            Assert.That(rules.BoardLimit, Is.EqualTo(7));
        }

        [Test]
        public void ReadGacha_WhenBodyIsNotObject_Throws()
        {
            ConfigReadException exception = Assert.Throws<ConfigReadException>(
                () => ConfigJsonReader.ReadGacha(JsonValue.Parse("{\"schemaVersion\":1,\"gacha\":5}"), "gacha.json"))!;

            Assert.That(exception.Reason, Does.Contain("应为对象"));
        }

        [Test]
        public void ReadBundle_WhenAllDocumentsProvided_BuildsBundle()
        {
            ConfigBundle bundle = ConfigJsonReader.ReadBundle(
                JsonValue.Parse(CardJson),
                JsonValue.Parse("{\"schemaVersion\":1,\"heroes\":[]}"),
                JsonValue.Parse("{\"schemaVersion\":1,\"heroPowers\":[]}"),
                JsonValue.Parse("{\"schemaVersion\":1,\"rarityWeights\":[]}"),
                JsonValue.Parse("{\"schemaVersion\":1,\"gacha\":{\"packSize\":5,\"coinCost\":100,\"pityCount\":10,\"pityRarity\":\"Legendary\"}}"),
                JsonValue.Parse("{\"schemaVersion\":1,\"rules\":{\"heroHealth\":30,\"handLimit\":10,\"boardLimit\":7,\"manaLimit\":10}}"));

            Assert.That(bundle.Cards.Count, Is.EqualTo(1));
            Assert.That(bundle.Gacha.PackSize, Is.EqualTo(5));
            Assert.That(bundle.Rules.HeroHealth, Is.EqualTo(30));
        }
    }
}
