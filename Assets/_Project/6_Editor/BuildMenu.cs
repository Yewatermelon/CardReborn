using System;
using System.IO;
using Card.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Card.Editor
{
    /// <summary>
    /// Windows x64 构建入口（M14-T4；Docs/02 M14-T4 双开工具与文档）：
    /// 菜单一键出包 + <c>-executeMethod</c> 批处理入口。产物 Builds/Windows/（不入库）。
    /// 双开验证：用同一构建起两实例（-host / -join 参数区分角色，见 Docs/HANDOFF §4.3）。
    /// </summary>
    public static class BuildMenu
    {
        private const string OutputExe = "Builds/Windows/CardReborn.exe";

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Battle.unity"
        };

        [MenuItem("Card/Build/Windows x64")]
        public static void BuildFromMenu()
        {
            BuildWindows();
        }

        /// <summary>批处理入口：-executeMethod Card.Editor.BuildMenu.BuildWindowsForAutomation。</summary>
        public static void BuildWindowsForAutomation()
        {
            BuildWindows();
        }

        private static void BuildWindows()
        {
            string output = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", OutputExe));
            string? dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                // 批处理约定：-executeMethod 未捕获异常 → Unity 退出码非 0，CI 可判定失败。
                GameLog.Error(LogChannel.Boot, "Windows 构建失败：" + report.summary.result);
                throw new InvalidOperationException("Windows 构建失败：" + report.summary.result);
            }

            GameLog.Info(LogChannel.Boot, "Windows 构建完成：" + output
                + "（" + report.summary.totalSize + " 字节）");
        }
    }
}
