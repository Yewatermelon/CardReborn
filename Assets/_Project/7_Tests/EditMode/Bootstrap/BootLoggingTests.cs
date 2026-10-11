using System;
using System.IO;
using System.Text.RegularExpressions;
using Card.Bootstrap;
using Card.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// 启动日志落盘测试：文件行格式（级别/通道/消息）、控制台桥接分级、
    /// Configure 建目录与开机标记、ConfigureOnce 幂等。测试隔离依赖
    /// GameLog.ResetForTests / BootLogging.ResetForTests（internal + InternalsVisibleTo）。
    /// </summary>
    [TestFixture]
    public class BootLoggingTests
    {
        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            GameLog.ResetForTests();
            BootLogging.ResetForTests();
            _directory = Path.Combine(Path.GetTempPath(), "CardBootLoggingTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            GameLog.ResetForTests();
            BootLogging.ResetForTests();
        }

        [Test]
        public void Write_WritesLineWithLevelChannelAndMessage()
        {
            string filePath = Path.Combine(_directory, "sink.log");
            using (var sink = new UnityFileLogSink(filePath, bridgeToConsole: false))
            {
                sink.Write(LogLevel.Warn, LogChannel.Network, "对端断开");
                sink.Write(LogLevel.Error, LogChannel.Rule, "非法命令");
            }

            string[] lines = File.ReadAllLines(filePath);
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Does.Match(@"^\d{2}:\d{2}:\d{2}\.\d{3} \[Warn\]\[Network\] 对端断开$"));
            Assert.That(lines[1], Does.Match(@"^\d{2}:\d{2}:\d{2}\.\d{3} \[Error\]\[Rule\] 非法命令$"));
        }

        [Test]
        public void Write_BridgesErrorToUnityConsole()
        {
            string filePath = Path.Combine(_directory, "bridge.log");
            using (var sink = new UnityFileLogSink(filePath, bridgeToConsole: true))
            {
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Card\] \d{2}:\d{2}:\d{2}\.\d{3} \[Error\]\[Rule\] boom$"));
                sink.Write(LogLevel.Error, LogChannel.Rule, "boom");
            }

            Assert.That(File.Exists(filePath), Is.True);
        }

        [Test]
        public void Configure_CreatesDirectory_WritesBootLine_AndSetsFlag()
        {
            BootLogging.Configure(_directory);

            Assert.That(BootLogging.Configured, Is.True);
            Assert.That(Directory.Exists(_directory), Is.True);
            string[] logs = Directory.GetFiles(_directory, "*.log");
            Assert.That(logs, Has.Length.EqualTo(1));
            string content = ReadAllTextShared(logs[0]);
            Assert.That(content, Does.Contain("[Info][Boot] 日志落盘已启用"));
        }

        /// <summary>共享读：BootLogging 的 StreamWriter 持有写入句柄（FileShare.Read），
        /// File.ReadAllText 的 FileShare.Read 与既有写句柄冲突（Sharing violation），须放宽共享。</summary>
        private static string ReadAllTextShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        [Test]
        public void ConfigureOnce_SecondCallIsNoOp()
        {
            string logsDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "Logs");
            int before = Directory.Exists(logsDirectory) ? Directory.GetFiles(logsDirectory, "*.log").Length : 0;

            BootLogging.ConfigureOnce();
            BootLogging.ConfigureOnce();

            Assert.That(BootLogging.Configured, Is.True);
            int after = Directory.GetFiles(logsDirectory, "*.log").Length;
            Assert.That(after, Is.EqualTo(before + 1), "重复 ConfigureOnce 不得再建日志文件");
        }
    }
}
