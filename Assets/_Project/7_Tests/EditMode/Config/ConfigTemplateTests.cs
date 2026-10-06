using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>
    /// M2-T2：源表模板自身的合法性。
    /// 覆盖表头一致性、枚举/关键词/整数/布尔解析、外键与唯一性——模板写错必须在这里就失败。
    /// </summary>
    public sealed class ConfigTemplateTests
    {
        [Test]
        public void Templates_WhenLoaded_AllSixExistAndParse()
        {
            string[] files =
            {
                ConfigTemplates.CardsFile,
                ConfigTemplates.HeroesFile,
                ConfigTemplates.HeroPowersFile,
                ConfigTemplates.RarityWeightsFile,
                ConfigTemplates.GachaConfigFile,
                ConfigTemplates.RulesFile
            };

            for (int i = 0; i < files.Length; i++)
            {
                CsvTable table = ConfigTemplates.Load(files[i]);

                Assert.That(table.ColumnCount, Is.GreaterThan(0), files[i] + " 应有表头");
                Assert.That(table.RowCount, Is.GreaterThan(0), files[i] + " 应至少有一行示例数据");
            }
        }

        [Test]
        public void Cards_HeaderMatchesContractColumns()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.CardsFile);

            Assert.That(table.Header, Is.EqualTo(new[]
            {
                "Id", "Key", "NameKey", "DescKey", "Cost", "Type", "Rarity", "Class",
                "Attack", "Health", "Keywords", "TargetRule", "Effects", "SetKey",
                "ArtKey", "AudioKey", "Enabled"
            }));
        }

        [Test]
        public void Cards_EveryRowParsesIntoValidValues()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.CardsFile);

            for (int row = 0; row < table.RowCount; row++)
            {
                string where = "Cards.csv 第 " + (row + 2) + " 行";

                Assert.That(int.TryParse(table.GetCell(row, "Id"), out int id), Is.True, where + " Id 必须是整数");
                Assert.That(id, Is.GreaterThan(0), where + " Id 必须为正");
                Assert.That(table.GetCell(row, "Key"), Is.Not.Empty, where + " Key 不能为空");
                Assert.That(table.GetCell(row, "NameKey"), Is.Not.Empty, where + " NameKey 不能为空");
                Assert.That(int.TryParse(table.GetCell(row, "Cost"), out int cost), Is.True, where + " Cost 必须是整数");
                Assert.That(cost, Is.GreaterThanOrEqualTo(0), where + " Cost 不能为负");

                Assert.That(
                    ConfigTokens.TryParseEnum<CardType>(table.GetCell(row, "Type"), out _),
                    Is.True, where + " Type 非法");
                Assert.That(
                    ConfigTokens.TryParseEnum<CardRarity>(table.GetCell(row, "Rarity"), out _),
                    Is.True, where + " Rarity 非法");
                Assert.That(
                    ConfigTokens.TryParseEnum<CardClass>(table.GetCell(row, "Class"), out _),
                    Is.True, where + " Class 非法");
                Assert.That(
                    ConfigTokens.TryParseEnum<TargetRule>(table.GetCell(row, "TargetRule"), out _),
                    Is.True, where + " TargetRule 非法");
                Assert.That(
                    KeywordTokens.TryParse(table.GetCell(row, "Keywords"), out _, out string? badToken),
                    Is.True, where + " Keywords 含非法值：" + badToken);

                string enabled = table.GetCell(row, "Enabled");
                Assert.That(
                    enabled == "TRUE" || enabled == "FALSE" || enabled == "1" || enabled == "0",
                    Is.True, where + " Enabled 只接受 TRUE/FALSE/1/0，实际为 " + enabled);
            }
        }

        [Test]
        public void Cards_KeysAndIdsAreUnique()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.CardsFile);
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> keys = new HashSet<string>();

            for (int row = 0; row < table.RowCount; row++)
            {
                Assert.That(ids.Add(table.GetCell(row, "Id")), Is.True, "Cards.csv Id 重复：" + table.GetCell(row, "Id"));
                Assert.That(keys.Add(table.GetCell(row, "Key")), Is.True, "Cards.csv Key 重复：" + table.GetCell(row, "Key"));
            }
        }

        [Test]
        public void Cards_MinionsHavePositiveStatsAndSpellsAreZero()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.CardsFile);

            for (int row = 0; row < table.RowCount; row++)
            {
                ConfigTokens.TryParseEnum<CardType>(table.GetCell(row, "Type"), out CardType type);
                int.TryParse(table.GetCell(row, "Attack"), out int attack);
                int.TryParse(table.GetCell(row, "Health"), out int health);
                string where = "Cards.csv 第 " + (row + 2) + " 行";

                Assert.That(attack, Is.GreaterThanOrEqualTo(0), where + " Attack 不能为负");
                Assert.That(health, Is.GreaterThanOrEqualTo(0), where + " Health 不能为负");

                if (type == CardType.Minion)
                {
                    Assert.That(health, Is.GreaterThan(0), where + " 随从必须有生命");
                }
                else
                {
                    Assert.That(attack, Is.EqualTo(0), where + " 非法术卡不应有攻击力");
                    Assert.That(health, Is.EqualTo(0), where + " 非法术卡不应有生命值");
                }
            }
        }

        [Test]
        public void Heroes_HeaderMatchesContractColumns()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.HeroesFile);

            Assert.That(table.Header, Is.EqualTo(new[]
            {
                "Id", "Key", "NameKey", "Health", "HeroPowerKey", "Class"
            }));
        }

        [Test]
        public void Heroes_HeroPowerKeysExistInHeroPowers()
        {
            CsvTable heroes = ConfigTemplates.Load(ConfigTemplates.HeroesFile);
            CsvTable powers = ConfigTemplates.Load(ConfigTemplates.HeroPowersFile);
            HashSet<string> powerKeys = new HashSet<string>();

            for (int row = 0; row < powers.RowCount; row++)
            {
                powerKeys.Add(powers.GetCell(row, "Key"));
            }

            for (int row = 0; row < heroes.RowCount; row++)
            {
                string key = heroes.GetCell(row, "HeroPowerKey");

                Assert.That(key, Is.Not.Empty, "Heroes.csv 第 " + (row + 2) + " 行缺少英雄技能");
                Assert.That(powerKeys.Contains(key), Is.True, "Heroes.csv 引用了不存在的技能：" + key);
                Assert.That(
                    ConfigTokens.TryParseEnum<CardClass>(heroes.GetCell(row, "Class"), out _),
                    Is.True, "Heroes.csv 第 " + (row + 2) + " 行 Class 非法");
                Assert.That(int.TryParse(heroes.GetCell(row, "Health"), out int health), Is.True);
                Assert.That(health, Is.GreaterThan(0), "英雄生命必须为正");
            }
        }

        [Test]
        public void HeroPowers_EveryRowParsesIntoValidValues()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.HeroPowersFile);

            Assert.That(table.Header, Is.EqualTo(new[] { "Id", "Key", "Cost", "TargetRule", "Effects" }));

            for (int row = 0; row < table.RowCount; row++)
            {
                string where = "HeroPowers.csv 第 " + (row + 2) + " 行";

                Assert.That(table.GetCell(row, "Key"), Is.Not.Empty, where + " Key 不能为空");
                Assert.That(int.TryParse(table.GetCell(row, "Cost"), out int cost), Is.True, where + " Cost 必须是整数");
                Assert.That(cost, Is.GreaterThan(0), where + " Cost 必须为正");
                Assert.That(
                    ConfigTokens.TryParseEnum<TargetRule>(table.GetCell(row, "TargetRule"), out _),
                    Is.True, where + " TargetRule 非法");
                Assert.That(table.GetCell(row, "Effects"), Is.Not.Empty, where + " 技能必须有至少一个效果");
            }
        }

        [Test]
        public void RarityWeights_CoversAllRaritiesWithPositiveWeights()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.RarityWeightsFile);

            Assert.That(table.Header, Is.EqualTo(new[] { "Rarity", "Weight", "MinPerPack" }));

            HashSet<CardRarity> seen = new HashSet<CardRarity>();
            for (int row = 0; row < table.RowCount; row++)
            {
                string where = "RarityWeights.csv 第 " + (row + 2) + " 行";
                ConfigTokens.TryParseEnum<CardRarity>(table.GetCell(row, "Rarity"), out CardRarity rarity);
                int.TryParse(table.GetCell(row, "Weight"), out int weight);
                int.TryParse(table.GetCell(row, "MinPerPack"), out int minPerPack);

                seen.Add(rarity);
                Assert.That(weight, Is.GreaterThan(0), where + " Weight 必须为正");
                Assert.That(minPerPack, Is.GreaterThanOrEqualTo(0), where + " MinPerPack 不能为负");
            }

            Assert.That(seen.Count, Is.EqualTo(4), "四个稀有度都必须有配置行");
        }

        [Test]
        public void GachaConfig_HasSingleValidRow()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.GachaConfigFile);

            Assert.That(table.Header, Is.EqualTo(new[] { "PackSize", "CoinCost", "PityCount", "PityRarity" }));
            Assert.That(table.RowCount, Is.EqualTo(1), "抽卡配置是单行表");
            Assert.That(int.Parse(table.GetCell(0, "PackSize")), Is.GreaterThan(0));
            Assert.That(int.Parse(table.GetCell(0, "CoinCost")), Is.GreaterThan(0));
            Assert.That(int.Parse(table.GetCell(0, "PityCount")), Is.GreaterThan(0));
            Assert.That(
                ConfigTokens.TryParseEnum<CardRarity>(table.GetCell(0, "PityRarity"), out _),
                Is.True, "PityRarity 非法");
        }

        [Test]
        public void Rules_HasSingleRowWithinPrdRanges()
        {
            CsvTable table = ConfigTemplates.Load(ConfigTemplates.RulesFile);

            Assert.That(
                table.Header,
                Is.EqualTo(new[]
                {
                    "HeroHealth", "HandLimit", "BoardLimit", "ManaLimit",
                    "DeckSize", "StartingHandFirst", "StartingHandSecond", "TheCoinCardKey"
                }));
            Assert.That(table.RowCount, Is.EqualTo(1), "规则表是单行表");

            int heroHealth = int.Parse(table.GetCell(0, "HeroHealth"));
            int handLimit = int.Parse(table.GetCell(0, "HandLimit"));
            int boardLimit = int.Parse(table.GetCell(0, "BoardLimit"));
            int manaLimit = int.Parse(table.GetCell(0, "ManaLimit"));
            int deckSize = int.Parse(table.GetCell(0, "DeckSize"));
            int startFirst = int.Parse(table.GetCell(0, "StartingHandFirst"));
            int startSecond = int.Parse(table.GetCell(0, "StartingHandSecond"));

            Assert.That(heroHealth, Is.InRange(1, 100));
            Assert.That(handLimit, Is.InRange(1, 10), "手牌上限不得超过 10（Docs/01 第 3.2 节）");
            Assert.That(boardLimit, Is.InRange(1, 7), "场面上限不得超过 7（Docs/01 第 3.3 节）");
            Assert.That(manaLimit, Is.InRange(1, 10), "法力上限不得超过 10");
            Assert.That(deckSize, Is.EqualTo(30), "卡组严格 30 张（Docs/01 §7.2）");
            Assert.That(startFirst, Is.InRange(0, handLimit));
            Assert.That(startSecond, Is.InRange(0, handLimit));
            Assert.That(table.GetCell(0, "TheCoinCardKey"), Is.EqualTo("NEUTRAL_THE_COIN"));
        }
    }
}
