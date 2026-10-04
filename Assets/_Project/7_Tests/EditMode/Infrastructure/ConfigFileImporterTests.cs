using System;
using System.IO;
using Card.Core;
using Card.Domain.Config;
using Card.Infrastructure.Config;
using Card.Tests.EditMode.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Infrastructure
{
    /// <summary>M2-T3 会话 B：CSV 源表 → JSON 生成物的文件级导入（临时目录，不碰仓库）。</summary>
    public sealed class ConfigFileImporterTests
    {
        private string _root = string.Empty;
        private string _sourceDirectory = string.Empty;
        private string _outputDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CardRebornImportTest_" + Guid.NewGuid().ToString("N"));
            _sourceDirectory = Path.Combine(_root, "Excel");
            _outputDirectory = Path.Combine(_root, "Config");
            Directory.CreateDirectory(_sourceDirectory);
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
        public void Import_WhenRealTemplatesCopied_WritesAllSixJsonFiles()
        {
            CopyRealTemplates();

            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.Succeeded, Is.True, result.ToText());
            Assert.That(result.WrittenFiles.Count, Is.EqualTo(6));
            AssertFile("cards.json");
            AssertFile("heroes.json");
            AssertFile("hero_powers.json");
            AssertFile("rarity_weights.json");
            AssertFile("gacha.json");
            AssertFile("rules.json");
        }

        [Test]
        public void Import_WhenValid_GeneratedFilesCarryVersionAndNoEditMarker()
        {
            CopyRealTemplates();

            ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            string cards = File.ReadAllText(Path.Combine(_outputDirectory, "cards.json"));
            Assert.That(cards, Does.StartWith("{\n  \"_generated\": "), "生成物首行必须是'勿手改'标记");
            Assert.That(cards, Does.Contain("\"schemaVersion\": " + ConfigSchema.CurrentVersion));
            Assert.That(cards, Does.Contain("\"key\": \"MAGE_FIREBALL\""));
        }

        [Test]
        public void Import_WhenCalledTwice_WritesIdenticalContent()
        {
            CopyRealTemplates();
            ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);
            string first = File.ReadAllText(Path.Combine(_outputDirectory, "cards.json"));

            ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);
            string second = File.ReadAllText(Path.Combine(_outputDirectory, "cards.json"));

            Assert.That(second, Is.EqualTo(first), "同源表重复导入必须得到逐字节相同的结果");
        }

        [Test]
        public void Import_WhenValidationFails_WritesNothing()
        {
            CopyRealTemplates();
            File.WriteAllText(
                Path.Combine(_sourceDirectory, "Cards.csv"),
                "Id,Key,NameKey,DescKey,Cost,Type,Rarity,Class,Attack,Health,Keywords,TargetRule,Effects,SetKey,ArtKey,AudioKey,Enabled\n" +
                "1,BROKEN,CARD_X_NAME,CARD_X_DESC,abc,Minion,Common,Neutral,2,3,,None,,Core,art_x,sfx_x,TRUE\n");

            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Report.HasErrors, Is.True);
            Assert.That(result.WrittenFiles, Is.Empty);
            Assert.That(Directory.Exists(_outputDirectory), Is.False, "校验失败时连目录都不应创建");
        }

        [Test]
        public void Import_WhenValidationFails_LeavesExistingOutputUntouched()
        {
            CopyRealTemplates();
            Directory.CreateDirectory(_outputDirectory);
            string sentinel = Path.Combine(_outputDirectory, "cards.json");
            File.WriteAllText(sentinel, "SENTINEL");
            File.WriteAllText(
                Path.Combine(_sourceDirectory, "Rules.csv"),
                "HeroHealth,HandLimit,BoardLimit,ManaLimit\n30,99,7,10\n");

            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("SENTINEL"), "失败不得覆盖既有生成物");
        }

        [Test]
        public void Import_WhenSourceDirectoryIsEmpty_ReportsSixMissingTables()
        {
            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Report.Count, Is.EqualTo(6), result.ToText());
            Assert.That(result.Report.Issues[0].Message, Is.EqualTo("源表缺失"));
        }

        [Test]
        public void Import_WhenCsvIsMalformed_ReportsCsvFormatIssue()
        {
            CopyRealTemplates();
            File.WriteAllText(
                Path.Combine(_sourceDirectory, "HeroPowers.csv"),
                "Id,Key,Cost,TargetRule,Effects\n1,HERO_POWER_FIREBALL,2,Any,DamageEffect:1,EXTRA\n");

            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Report.Issues[0].Message, Does.Contain("CSV 格式错误"));
            Assert.That(result.Report.Issues[0].TableName, Is.EqualTo("HeroPowers.csv"));
        }

        [Test]
        public void Import_WhenOutputDirectoryMissing_CreatesIt()
        {
            CopyRealTemplates();
            Assert.That(Directory.Exists(_outputDirectory), Is.False);

            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.Succeeded, Is.True, result.ToText());
            Assert.That(Directory.Exists(_outputDirectory), Is.True);
        }

        [Test]
        public void Import_WhenSourceDirectoryIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ConfigFileImporter.Import(null!, _outputDirectory));
            Assert.Throws<ArgumentNullException>(() => ConfigFileImporter.Import(_sourceDirectory, null!));
        }

        [Test]
        public void Result_WhenFailed_ToTextExplainsNothingWasWritten()
        {
            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.ToText(), Does.StartWith("导入失败，未写出任何文件。"));
            Assert.That(result.ToString(), Is.EqualTo(result.ToText()));
        }

        [Test]
        public void Result_WhenSucceeded_ToTextListsWrittenFiles()
        {
            CopyRealTemplates();

            ConfigImportResult result = ConfigFileImporter.Import(_sourceDirectory, _outputDirectory);

            Assert.That(result.ToText(), Does.StartWith("导入成功，写出 6 个文件"));
            Assert.That(result.ToText(), Does.Contain("cards.json"));
        }

        private void CopyRealTemplates()
        {
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
        }

        private void AssertFile(string fileName)
        {
            string path = Path.Combine(_outputDirectory, fileName);
            Assert.That(File.Exists(path), Is.True, "缺少生成物：" + fileName);
            Assert.That(File.ReadAllText(path), Is.Not.Empty);
        }
    }
}
