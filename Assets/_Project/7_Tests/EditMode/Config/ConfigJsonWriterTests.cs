using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T3：契约 → JSON 生成物（含逐字符 golden 与真实模板端到端）。</summary>
    public sealed class ConfigJsonWriterTests
    {
        [Test]
        public void WriteCards_WhenSingleCard_MatchesExpectedJsonExactly()
        {
            CardDefinition card = new CardDefinition
            {
                Id = 1,
                Key = "NEUTRAL_PANGO",
                NameKey = "CARD_001_NAME",
                DescKey = "CARD_001_DESC",
                Cost = 2,
                Type = CardType.Minion,
                Rarity = CardRarity.Common,
                Class = CardClass.Neutral,
                Attack = 2,
                Health = 3,
                Keywords = Keyword.Taunt | Keyword.Charge,
                TargetRule = TargetRule.None,
                Effects = new[] { "DamageEffect:2" },
                SetKey = "Core",
                ArtKey = "art_card_001",
                AudioKey = "sfx_play_001",
                Enabled = true
            };

            string expected = string.Join("\n", new[]
            {
                "{",
                "  \"_generated\": \"由 Tools/Card/导入配置 生成，请勿手改\",",
                "  \"schemaVersion\": 1,",
                "  \"cards\": [",
                "    {",
                "      \"id\": 1,",
                "      \"key\": \"NEUTRAL_PANGO\",",
                "      \"nameKey\": \"CARD_001_NAME\",",
                "      \"descKey\": \"CARD_001_DESC\",",
                "      \"cost\": 2,",
                "      \"type\": \"Minion\",",
                "      \"rarity\": \"Common\",",
                "      \"class\": \"Neutral\",",
                "      \"attack\": 2,",
                "      \"health\": 3,",
                "      \"keywords\": \"Taunt|Charge\",",
                "      \"targetRule\": \"None\",",
                "      \"effects\": [",
                "        \"DamageEffect:2\"",
                "      ],",
                "      \"setKey\": \"Core\",",
                "      \"artKey\": \"art_card_001\",",
                "      \"audioKey\": \"sfx_play_001\",",
                "      \"enabled\": true",
                "    }",
                "  ]",
                "}"
            });

            Assert.That(ConfigJsonWriter.WriteCards(new[] { card }).ToJson(), Is.EqualTo(expected));
        }

        [Test]
        public void WriteCards_WhenNoCards_WritesEmptyArray()
        {
            string json = ConfigJsonWriter.WriteCards(Array.Empty<CardDefinition>()).ToJson();

            Assert.That(
                json,
                Is.EqualTo(
                    "{\n  \"_generated\": \"由 Tools/Card/导入配置 生成，请勿手改\"," +
                    "\n  \"schemaVersion\": 1,\n  \"cards\": []\n}"));
        }

        [Test]
        public void WriteCards_WhenListIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => ConfigJsonWriter.WriteCards(null!));
        }

        [Test]
        public void WriteHeroes_WhenValid_HasSchemaVersionEnvelope()
        {
            List<HeroDefinition> heroes = new List<HeroDefinition>
            {
                new HeroDefinition
                {
                    Id = 1,
                    Key = "HERO_MAGE",
                    NameKey = "HERO_001_NAME",
                    Health = 30,
                    HeroPowerKey = "HERO_POWER_FIREBALL",
                    Class = CardClass.Mage
                }
            };

            string json = ConfigJsonWriter.WriteHeroes(heroes).ToJson();

            Assert.That(json, Does.Contain("\n  \"schemaVersion\": 1,\n  \"heroes\": ["));
            Assert.That(json, Does.StartWith("{\n  \"_generated\": "));
            Assert.That(json, Does.Contain("\"heroPowerKey\": \"HERO_POWER_FIREBALL\""));
            Assert.That(json, Does.Contain("\"class\": \"Mage\""));
        }

        [Test]
        public void WriteHeroPowersAndWeights_WhenValid_WriteEntries()
        {
            List<HeroPowerDefinition> powers = new List<HeroPowerDefinition>
            {
                new HeroPowerDefinition
                {
                    Id = 1,
                    Key = "HERO_POWER_FIREBALL",
                    Cost = 2,
                    TargetRule = TargetRule.Any,
                    Effects = new[] { "DamageEffect:1" }
                }
            };

            List<RarityWeight> weights = new List<RarityWeight>
            {
                new RarityWeight { Rarity = CardRarity.Legendary, Weight = 2, MinPerPack = 0 }
            };

            Assert.That(
                ConfigJsonWriter.WriteHeroPowers(powers).ToJson(),
                Does.Contain("\"heroPowers\": ["));
            Assert.That(
                ConfigJsonWriter.WriteRarityWeights(weights).ToJson(),
                Does.Contain("\"rarityWeights\": ["));
        }

        [Test]
        public void WriteGachaAndRules_WhenValid_WriteSingleObjectBody()
        {
            GachaConfig gacha = new GachaConfig
            {
                PackSize = 5,
                CoinCost = 100,
                PityCount = 10,
                PityRarity = CardRarity.Legendary
            };

            RulesConfig rules = new RulesConfig
            {
                HeroHealth = 30,
                HandLimit = 10,
                BoardLimit = 7,
                ManaLimit = 10
            };

            Assert.That(
                ConfigJsonWriter.WriteGacha(gacha).ToJson(),
                Does.Contain("\"gacha\": {\n    \"packSize\": 5,"));
            Assert.That(
                ConfigJsonWriter.WriteRules(rules).ToJson(),
                Does.Contain("\"rules\": {\n    \"heroHealth\": 30,"));
        }

        [Test]
        public void EndToEnd_WhenRealCardTemplateMapped_WritesEveryCard()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.CardsFile);
            List<CardDefinition> cards = new List<CardDefinition>(table.RowCount);
            for (int row = 0; row < table.RowCount; row++)
            {
                cards.Add(ConfigRowParser.ParseCard(table, row, ConfigTemplates.CardsFile));
            }

            string json = ConfigJsonWriter.WriteCards(cards).ToJson();

            Assert.That(json, Does.Contain("\n  \"schemaVersion\": 1,\n  \"cards\": ["));
            Assert.That(json, Does.StartWith("{\n  \"_generated\": "));
            Assert.That(cards.Count, Is.EqualTo(table.RowCount));
            for (int i = 0; i < cards.Count; i++)
            {
                Assert.That(json, Does.Contain("\"key\": \"" + cards[i].Key + "\""), "缺少卡牌：" + cards[i].Key);
            }
        }

        [Test]
        public void EndToEnd_WhenRealSingleRowTemplatesMapped_WritesEnvelopes()
        {
            CsvTable gachaTable = ConfigTemplates.Load(ConfigTemplates.GachaConfigFile);
            CsvTable rulesTable = ConfigTemplates.Load(ConfigTemplates.RulesFile);

            GachaConfig gacha = ConfigRowParser.ParseGachaConfig(gachaTable, 0, ConfigTemplates.GachaConfigFile);
            RulesConfig rules = ConfigRowParser.ParseRules(rulesTable, 0, ConfigTemplates.RulesFile);

            Assert.That(
                ConfigJsonWriter.WriteGacha(gacha).ToJson(),
                Does.Contain("\n  \"schemaVersion\": " + ConfigSchema.CurrentVersion + ","));
            Assert.That(
                ConfigJsonWriter.WriteRules(rules).ToJson(),
                Does.Contain("\n  \"schemaVersion\": " + ConfigSchema.CurrentVersion + ","));
        }

        [Test]
        public void EndToEnd_WhenRealHeroAndPowerTemplatesMapped_MatchKeys()
        {
            CsvTable heroesTable = ConfigTemplates.Load(ConfigTemplates.HeroesFile);
            CsvTable powersTable = ConfigTemplates.Load(ConfigTemplates.HeroPowersFile);

            List<HeroDefinition> heroes = new List<HeroDefinition>(heroesTable.RowCount);
            for (int row = 0; row < heroesTable.RowCount; row++)
            {
                heroes.Add(ConfigRowParser.ParseHero(heroesTable, row, ConfigTemplates.HeroesFile));
            }

            List<HeroPowerDefinition> powers = new List<HeroPowerDefinition>(powersTable.RowCount);
            for (int row = 0; row < powersTable.RowCount; row++)
            {
                powers.Add(ConfigRowParser.ParseHeroPower(powersTable, row, ConfigTemplates.HeroPowersFile));
            }

            string heroJson = ConfigJsonWriter.WriteHeroes(heroes).ToJson();
            string powerJson = ConfigJsonWriter.WriteHeroPowers(powers).ToJson();

            for (int i = 0; i < powers.Count; i++)
            {
                Assert.That(powerJson, Does.Contain("\"key\": \"" + powers[i].Key + "\""));
            }

            for (int i = 0; i < heroes.Count; i++)
            {
                Assert.That(heroJson, Does.Contain("\"heroPowerKey\": \"" + heroes[i].HeroPowerKey + "\""));
            }
        }
    }
}
