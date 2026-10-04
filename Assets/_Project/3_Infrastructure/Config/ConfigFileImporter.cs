using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Card.Core;
using Card.Domain.Config;

namespace Card.Infrastructure.Config
{
    /// <summary>
    /// CSV 源表 → JSON 生成物（Docs/03 第 9.1 节）。
    /// 只做文件 I/O 编排：解析在 <see cref="CsvTable"/>、校验在 <see cref="ConfigValidator"/>、
    /// 序列化在 <see cref="ConfigJsonWriter"/>。校验不通过时**不写任何文件**（不产生半成品数据）。
    /// 每个文件先写 <c>.tmp</c> 再替换，避免中途失败留下截断的生成物。
    /// </summary>
    public static class ConfigFileImporter
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>执行导入；返回结果里带上校验报告与写出的文件列表。</summary>
        public static ConfigImportResult Import(string sourceDirectory, string outputDirectory)
        {
            Guard.NotNull(sourceDirectory, nameof(sourceDirectory));
            Guard.NotNull(outputDirectory, nameof(outputDirectory));

            List<ConfigValidationIssue> loadingIssues = new List<ConfigValidationIssue>();
            ConfigSourceSet sources = new ConfigSourceSet(
                TryLoad(sourceDirectory, "Cards.csv", loadingIssues),
                TryLoad(sourceDirectory, "Heroes.csv", loadingIssues),
                TryLoad(sourceDirectory, "HeroPowers.csv", loadingIssues),
                TryLoad(sourceDirectory, "RarityWeights.csv", loadingIssues),
                TryLoad(sourceDirectory, "GachaConfig.csv", loadingIssues),
                TryLoad(sourceDirectory, "Rules.csv", loadingIssues),
                loadingIssues);

            ConfigValidationReport report = ConfigValidator.Validate(sources);
            if (report.HasErrors)
            {
                return ConfigImportResult.Failure(report);
            }

            return ConfigImportResult.Success(report, WriteDocuments(outputDirectory, report.Result!));
        }

        private static List<string> WriteDocuments(string outputDirectory, ConfigBundle bundle)
        {
            (string FileName, JsonValue Document)[] documents =
            {
                (ConfigSchema.CardsFileName, ConfigJsonWriter.WriteCards(bundle.Cards)),
                (ConfigSchema.HeroesFileName, ConfigJsonWriter.WriteHeroes(bundle.Heroes)),
                (ConfigSchema.HeroPowersFileName, ConfigJsonWriter.WriteHeroPowers(bundle.HeroPowers)),
                (ConfigSchema.RarityWeightsFileName, ConfigJsonWriter.WriteRarityWeights(bundle.RarityWeights)),
                (ConfigSchema.GachaFileName, ConfigJsonWriter.WriteGacha(bundle.Gacha)),
                (ConfigSchema.RulesFileName, ConfigJsonWriter.WriteRules(bundle.Rules))
            };

            Directory.CreateDirectory(outputDirectory);

            List<string> written = new List<string>(documents.Length);
            for (int i = 0; i < documents.Length; i++)
            {
                written.Add(WriteDocument(outputDirectory, documents[i].FileName, documents[i].Document));
            }

            return written;
        }

        private static string WriteDocument(string outputDirectory, string fileName, JsonValue document)
        {
            string path = Path.Combine(outputDirectory, fileName);
            string temporaryPath = path + ".tmp";

            File.WriteAllText(temporaryPath, document.ToJson() + "\n", Utf8NoBom);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporaryPath, path);
            return path;
        }

        private static CsvTable? TryLoad(
            string sourceDirectory,
            string fileName,
            List<ConfigValidationIssue> issues)
        {
            string path = Path.Combine(sourceDirectory, fileName);
            if (!File.Exists(path))
            {
                return null; // 缺失由校验器报"源表缺失"，这里不重复报
            }

            try
            {
                return CsvTable.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (FormatException exception)
            {
                issues.Add(new ConfigValidationIssue(fileName, 0, string.Empty, "CSV 格式错误：" + exception.Message));
                return null;
            }
        }
    }
}
