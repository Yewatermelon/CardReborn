using System;
using System.IO;
using Card.Domain.Config;
using Card.Infrastructure.Config;
using Card.Tests.EditMode.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Infrastructure
{
    /// <summary>M2-T5：读取生成物（与导入器形成"写出 → 读回"闭环）。</summary>
    public sealed class ConfigFileLoaderTests
    {
        private string _root = string.Empty;
        private string _sourceDirectory = string.Empty;
        private string _outputDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CardRebornLoadTest_" + Guid.NewGuid().ToString("N"));
            _sourceDirectory = Path.Combine(_root, "Excel");
            _outputDirectory = Path.Combine(_root, "Config");
            Directory.CreateDirectory(_sourceDirectory);

            string[] files =
            {
                "Cards.csv", "Heroes.csv", "HeroPowers.csv",
                "RarityWeights.csv", "GachaConfig.csv", "Rules.csv"
            };

            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(
                    Path.Combine(ConfigTemplates.Root, "Config", "Excel", files[i]),
                    Path.Combine(_sourceDirectory, files[i]));
            }

            ConfigImportResult import = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);
            Assert.That(import.Succeeded, Is.True, import.ToText());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Load_WhenReadingImporterOutput_ReturnsSameContent()
        {
            ConfigLoadResult result = ConfigFileLoader.Load(_outputDirectory);

            Assert.That(result.Succeeded, Is.True, result.ToText());
            Assert.That(result.Bundle!.Cards.Count, Is.GreaterThanOrEqualTo(30), "首版内容目标见 ConfigContentTests");
            Assert.That(result.Bundle.Heroes.Count, Is.EqualTo(2));
            Assert.That(result.Bundle.HeroPowers.Count, Is.EqualTo(2));
            Assert.That(result.Bundle.RarityWeights.Count, Is.EqualTo(4));

            CardDefinition fireball = FindCard(result.Bundle, "MAGE_FIREBALL");
            Assert.That(fireball.Cost, Is.EqualTo(4), "读回来的值必须与源表一致");
            Assert.That(fireball.Class, Is.EqualTo(CardClass.Mage));
            Assert.That(fireball.TargetRule, Is.EqualTo(TargetRule.EnemyMinion));
            Assert.That(fireball.Effects, Is.EqualTo(new[] { "DamageEffect:6" }));
            Assert.That(fireball.Enabled, Is.True);

            CardDefinition treant = FindCard(result.Bundle, "NEUTRAL_TREANT");
            Assert.That(treant.Keywords, Is.EqualTo(Keyword.Deathrattle));
            Assert.That(treant.Rarity, Is.EqualTo(CardRarity.Epic));
        }

        [Test]
        public void Load_ThenBuildDatabase_QueriesWork()
        {
            ConfigLoadResult result = ConfigFileLoader.Load(_outputDirectory);

            CardDatabase database = new CardDatabase(result.Bundle!);

            Assert.That(database.RequireCard("MAGE_BLOCK").Cost, Is.EqualTo(3));
            Assert.That(database.RequireCard(1).Key, Is.EqualTo("NEUTRAL_PANGO"));
            Assert.That(database.FilterCards(cardClass: CardClass.Mage).Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(database.RequireHero("HERO_MAGE").Class, Is.EqualTo(CardClass.Mage));
            Assert.That(database.Gacha.PackSize, Is.EqualTo(5));
            Assert.That(database.Rules.ManaLimit, Is.EqualTo(10));
        }

        [Test]
        public void Load_WhenCommittedArtifactsUsed_Succeeds()
        {
            string committed = Path.Combine(ConfigTemplates.Root, "Assets", "_Project", "Config");

            ConfigLoadResult result = ConfigFileLoader.Load(committed);

            Assert.That(result.Succeeded, Is.True, result.ToText());
            Assert.That(result.Bundle!.Cards.Count, Is.GreaterThan(0), "仓库里的生成物应可直接使用");
        }

        [Test]
        public void Load_WhenFilesAreMissing_ReportsEachOne()
        {
            Directory.Delete(_outputDirectory, true);

            ConfigLoadResult result = ConfigFileLoader.Load(_outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Bundle, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(6), result.ToText());
            Assert.That(result.Errors[0], Does.Contain("缺少生成物"));
        }

        [Test]
        public void Load_WhenJsonIsCorrupt_ReportsParseError()
        {
            File.WriteAllText(Path.Combine(_outputDirectory, "cards.json"), "{ not json");

            ConfigLoadResult result = ConfigFileLoader.Load(_outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("cards.json"));
            Assert.That(result.Errors[0], Does.Contain("解析失败"));
        }

        [Test]
        public void Load_WhenSchemaVersionDiffers_ReportsMismatch()
        {
            string path = Path.Combine(_outputDirectory, "cards.json");
            File.WriteAllText(
                path,
                File.ReadAllText(path).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"));

            ConfigLoadResult result = ConfigFileLoader.Load(_outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("schema 版本不匹配"));
            Assert.That(result.Errors[0], Does.Contain("不做自动迁移"));
        }

        [Test]
        public void Load_WhenFieldIsMissing_ReportsJsonPath()
        {
            string path = Path.Combine(_outputDirectory, "cards.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"cost\": 2,", string.Empty));

            ConfigLoadResult result = ConfigFileLoader.Load(_outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("cards.json"));
            Assert.That(result.Errors[0], Does.Contain("cost"));
        }

        [Test]
        public void Load_WhenDirectoryIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ConfigFileLoader.Load(null!));
        }

        [Test]
        public void LoadResult_ToText_SummarizesOutcome()
        {
            ConfigLoadResult ok = ConfigFileLoader.Load(_outputDirectory);
            Assert.That(ok.ToText(), Is.EqualTo("配置加载成功。"));
            Assert.That(ok.ToString(), Is.EqualTo(ok.ToText()));

            Directory.Delete(_outputDirectory, true);
            ConfigLoadResult failed = ConfigFileLoader.Load(_outputDirectory);
            Assert.That(failed.ToText(), Does.StartWith("配置加载失败：共 6 个问题"));
        }

        private static CardDefinition FindCard(ConfigBundle bundle, string key)
        {
            for (int i = 0; i < bundle.Cards.Count; i++)
            {
                if (bundle.Cards[i].Key == key)
                {
                    return bundle.Cards[i];
                }
            }

            throw new InvalidOperationException("测试数据里没有卡牌：" + key);
        }
    }
}
