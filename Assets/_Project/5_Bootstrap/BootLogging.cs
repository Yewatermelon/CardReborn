using System;
using System.Globalization;
using System.IO;
using Card.Core;
using UnityEngine;

namespace Card.Bootstrap
{
    /// <summary>
    /// 组合根日志接线：把 <see cref="GameLog"/> 落盘到 persistentDataPath 并桥接
    /// Unity 控制台。构建版双开时每实例独立文件（文件名含进程 Id），复现联机问题后
    /// 可直接取日志排查（修复前构建版日志无任何应用层内容，联机缺陷无迹可循）。
    /// </summary>
    internal static class BootLogging
    {
        /// <summary>是否已配置（幂等开关；测试断言用）。</summary>
        internal static bool Configured { get; private set; }

        /// <summary>幂等配置：入口 Start 调用，重复调用为空操作。</summary>
        public static void ConfigureOnce()
        {
            if (Configured)
            {
                return;
            }

            string directory = Path.Combine(UnityEngine.Application.persistentDataPath, "Logs");
            Configure(directory);
        }

        /// <summary>配置到指定目录（测试注入用；生产走 ConfigureOnce）。</summary>
        internal static void Configure(string directory)
        {
            Directory.CreateDirectory(directory);
            string fileName = "log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-pid" + System.Diagnostics.Process.GetCurrentProcess().Id + ".log";
            GameLog.Configure(new UnityFileLogSink(Path.Combine(directory, fileName)), LogLevel.Info);
            Configured = true;
            GameLog.Info(LogChannel.Boot, "日志落盘已启用：" + fileName);
        }

        /// <summary>测试隔离：复位幂等标志（不影响已注册 sink；测试用 GameLog.ResetForTests 清后端）。</summary>
        internal static void ResetForTests()
        {
            Configured = false;
        }
    }

    /// <summary>ILogSink 的 Unity 文件后端：每条即写即刷（崩溃前保命）+ 桥接 Unity 控制台。</summary>
    internal sealed class UnityFileLogSink : ILogSink, IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly bool _bridgeToConsole;

        /// <param name="filePath">日志文件完整路径。</param>
        /// <param name="bridgeToConsole">是否同步输出到 Unity 控制台（测试可关，避免噪声）。</param>
        public UnityFileLogSink(string filePath, bool bridgeToConsole = true)
        {
            FilePath = Guard.NotNullOrWhiteSpace(filePath, nameof(filePath));
            _bridgeToConsole = bridgeToConsole;
            _writer = new StreamWriter(filePath, append: false) { AutoFlush = true };
        }

        /// <summary>日志文件完整路径（诊断用）。</summary>
        public string FilePath { get; }

        public void Write(LogLevel level, LogChannel channel, string text)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + " [" + level + "][" + channel + "] " + text;
            _writer.WriteLine(line);

            if (_bridgeToConsole)
            {
                WriteToConsole(level, line);
            }
        }

        /// <summary>关闭文件（测试收尾用；生产与进程同生命周期无需显式关闭）。</summary>
        public void Dispose()
        {
            _writer.Dispose();
        }

        private static void WriteToConsole(LogLevel level, string line)
        {
            switch (level)
            {
                case LogLevel.Error:
                    Debug.LogError("[Card] " + line);
                    break;
                case LogLevel.Warn:
                    Debug.LogWarning("[Card] " + line);
                    break;
                default:
                    Debug.Log("[Card] " + line);
                    break;
            }
        }
    }
}
