using System.IO;
using Card.Core;
using Card.Infrastructure.Config;
using UnityEditor;
using UnityEngine;

namespace Card.Editor.ConfigPipeline
{
    /// <summary>
    /// 一键导入：Config/Excel 的 CSV 源表 → Assets/_Project/Config 下的 JSON 生成物。
    /// 菜单只做"路径 + 提示"；真正的解析/校验/写出在 Card.Infrastructure（可被测试直接调用）。
    /// 失败时对话框列出**全部**问题（表名/行号/列名/原因），并且一个文件都不会写出。
    /// </summary>
    internal static class ConfigImportMenu
    {
        private const string MenuPath = "Tools/Card/导入配置";
        private const string SourceRelativePath = "Config/Excel";
        private const string OutputRelativePath = "Assets/_Project/Config";

        [MenuItem(MenuPath)]
        private static void ImportConfigFromMenu()
        {
            ImportConfig(showDialog: true);
        }

        /// <summary>
        /// 无界面入口（<c>-executeMethod</c> 与自动化使用）：不弹对话框，返回是否成功。
        /// 用法：<c>Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod Card.Editor.ConfigPipeline.ConfigImportMenu.ImportForAutomation</c>
        /// </summary>
        public static bool ImportForAutomation()
        {
            return ImportConfig(showDialog: false);
        }

        private static bool ImportConfig(bool showDialog)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string sourceDirectory = Path.Combine(projectRoot, SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string outputDirectory = Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));

            ConfigImportResult result = ConfigFileImporter.Import(sourceDirectory, outputDirectory);

            if (result.Succeeded)
            {
                GameLog.Info(LogChannel.Config, "配置导入成功，写出 " + result.WrittenFiles.Count + " 个文件");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("导入配置", result.ToText(), "好");
                }

                return true;
            }

            GameLog.Error(LogChannel.Config, "配置导入失败：\n" + result.Report.ToText());
            if (showDialog)
            {
                EditorUtility.DisplayDialog("导入配置失败", result.ToText(), "好");
            }

            return false;
        }
    }
}
