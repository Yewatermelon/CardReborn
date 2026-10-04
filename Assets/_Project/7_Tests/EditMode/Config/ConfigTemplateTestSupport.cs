using System;
using System.IO;
using Card.Core;

namespace Card.Tests.EditMode.Config
{
    /// <summary>
    /// 定位仓库根目录并加载配置源表。
    /// 刻意不使用 UnityEngine（Application.dataPath 等），这样同一批测试在
    /// Tools/Coverage 的无 Unity 工具链下也能运行。
    /// </summary>
    internal static class ConfigTemplates
    {
        public const string CardsFile = "Cards.csv";
        public const string HeroesFile = "Heroes.csv";
        public const string HeroPowersFile = "HeroPowers.csv";
        public const string RarityWeightsFile = "RarityWeights.csv";
        public const string GachaConfigFile = "GachaConfig.csv";
        public const string RulesFile = "Rules.csv";

        private static string? _root;

        public static string Root
        {
            get { return _root ??= FindRoot(); }
        }

        public static string PathOf(string fileName)
        {
            return Path.Combine(Root, "Config", "Excel", fileName);
        }

        public static CsvTable Load(string fileName)
        {
            string path = PathOf(fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("配置模板缺失：" + path, path);
            }

            return CsvTable.Parse(File.ReadAllText(path));
        }

        private static string FindRoot()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ConfigTemplates).Assembly.Location)
                ?? AppContext.BaseDirectory;
            string[] starts =
            {
                Directory.GetCurrentDirectory(),
                assemblyDirectory,
                AppContext.BaseDirectory
            };

            for (int i = 0; i < starts.Length; i++)
            {
                DirectoryInfo? current = new DirectoryInfo(starts[i]);
                while (current != null)
                {
                    if (Directory.Exists(Path.Combine(current.FullName, "Config", "Excel")) &&
                        File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            throw new InvalidOperationException(
                "无法定位仓库根目录：向上查找未找到同时包含 Config/Excel 与 AGENTS.md 的目录。");
        }
    }
}
