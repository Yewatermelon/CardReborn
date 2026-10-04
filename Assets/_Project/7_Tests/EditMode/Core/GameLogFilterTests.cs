using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T5：`GameLog` 的等级过滤、通道开关与热路径判断。</summary>
    public sealed class GameLogFilterTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLogTestHelper.Reset();
        }

        [Test]
        public void Info_WhenBelowMinimumLevel_IsFiltered()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Warn);

            GameLog.Info(LogChannel.Match, "普通信息");
            GameLog.Warn(LogChannel.Match, "警告");

            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warn));
        }

        [Test]
        public void Trace_WhenMinimumLevelIsInfo_IsFiltered()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);

            GameLog.Trace(LogChannel.Perf, "每帧细节");

            Assert.That(sink.Entries, Is.Empty);
        }

        [Test]
        public void Trace_WhenMinimumLevelIsTrace_IsDelivered()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Trace);

            GameLog.Trace(LogChannel.Perf, "每帧细节");

            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Trace));
        }

        [Test]
        public void SetChannelEnabled_WhenDisabled_FiltersOnlyThatChannel()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            GameLog.SetChannelEnabled(LogChannel.Ai, false);

            GameLog.Info(LogChannel.Ai, "AI 决策");
            GameLog.Info(LogChannel.Rule, "规则");

            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Rule));
        }

        [Test]
        public void SetChannelEnabled_WhenReEnabled_DeliversAgain()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            GameLog.SetChannelEnabled(LogChannel.Ai, false);
            GameLog.SetChannelEnabled(LogChannel.Ai, true);

            GameLog.Info(LogChannel.Ai, "AI 决策");

            Assert.That(sink.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void SetChannelEnabled_WhenChannelIsUndefined_ThrowsArgumentOutOfRangeException()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => GameLog.SetChannelEnabled((LogChannel)99, false))!;

            Assert.That(exception.ParamName, Is.EqualTo("channel"));
        }

        [Test]
        public void IsDefinedChannel_ReflectsEnumValues()
        {
            Assert.That(GameLog.IsDefinedChannel(LogChannel.Boot), Is.True);
            Assert.That(GameLog.IsDefinedChannel(LogChannel.Perf), Is.True);
            Assert.That(GameLog.IsDefinedChannel((LogChannel)99), Is.False);
            Assert.That(GameLog.IsDefinedChannel((LogChannel)(-1)), Is.False);
        }

        [Test]
        public void IsChannelEnabled_WhenChannelIsUndefined_StaysTrue()
        {
            Assert.That(GameLog.IsChannelEnabled((LogChannel)99), Is.True, "未知通道不丢日志");
        }

        [Test]
        public void IsEnabled_ReflectsLevelAndChannelCombinations()
        {
            GameLog.Configure(new RecordingLogSink(), LogLevel.Info);
            GameLog.SetChannelEnabled(LogChannel.Ai, false);

            Assert.That(GameLog.IsEnabled(LogChannel.Rule, LogLevel.Info), Is.True);
            Assert.That(GameLog.IsEnabled(LogChannel.Rule, LogLevel.Trace), Is.False);
            Assert.That(GameLog.IsEnabled(LogChannel.Ai, LogLevel.Error), Is.False, "通道关闭则等级再高也过滤");
        }

        [Test]
        public void Info_WhenFiltered_DoesNotValidateMessage()
        {
            GameLog.Configure(new RecordingLogSink(), LogLevel.Error);

            Assert.DoesNotThrow(
                () => GameLog.Info(LogChannel.Boot, null!),
                "被过滤的日志不应付出校验与字符串拼接成本（03 第 8 节规则 5）");
        }
    }
}
