using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T5：`GameLog` 的等级/通道过滤、契约与故障兜底。</summary>
    public sealed class GameLogTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLogTestHelper.Reset();
        }

        [Test]
        public void Info_WhenNotConfigured_IsSilentAndDoesNotThrow()
        {
            Assert.DoesNotThrow(() => GameLog.Info(LogChannel.Match, "未配置后端时不应抛异常"));
            Assert.That(GameLog.SinkFailureCount, Is.EqualTo(0));
        }

        [Test]
        public void Configure_WhenSinkIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => GameLog.Configure(null!, LogLevel.Info))!;

            Assert.That(exception.ParamName, Is.EqualTo("sink"));
        }

        [Test]
        public void Warn_WhenConfigured_DeliversLevelChannelAndMessage()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);

            GameLog.Warn(LogChannel.Rule, "法力不足：player=0");

            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warn));
            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Rule));
            Assert.That(sink.Entries[0].Message, Is.EqualTo("法力不足：player=0"));
        }

        [Test]
        public void Error_WhenConfigured_DeliversErrorLevel()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);

            GameLog.Error(LogChannel.Save, "存档损坏");

            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Save));
        }

        [Test]
        public void Disable_WhenCalled_StopsAllOutput()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            GameLog.Disable();

            GameLog.Error(LogChannel.Boot, "不应输出");

            Assert.That(sink.Entries, Is.Empty);
        }

        [Test]
        public void Configure_AfterDisable_ResumesOutput()
        {
            RecordingLogSink first = new RecordingLogSink();
            RecordingLogSink second = new RecordingLogSink();
            GameLog.Configure(first, LogLevel.Info);
            GameLog.Disable();
            GameLog.Configure(second, LogLevel.Info);

            GameLog.Info(LogChannel.Boot, "恢复了");

            Assert.That(first.Entries, Is.Empty);
            Assert.That(second.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void Configure_WhenCalledTwice_UsesLatestSinkAndLevel()
        {
            RecordingLogSink first = new RecordingLogSink();
            RecordingLogSink second = new RecordingLogSink();
            GameLog.Configure(first, LogLevel.Trace);
            GameLog.Configure(second, LogLevel.Error);

            GameLog.Warn(LogChannel.Boot, "低于新等级");
            GameLog.Error(LogChannel.Boot, "达到新等级");

            Assert.That(first.Entries, Is.Empty);
            Assert.That(GameLog.MinimumLevel, Is.EqualTo(LogLevel.Error));
            Assert.That(second.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void Info_WhenMessageIsNull_ThrowsArgumentNullException()
        {
            GameLog.Configure(new RecordingLogSink(), LogLevel.Info);

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => GameLog.Info(LogChannel.Boot, null!))!;

            Assert.That(exception.ParamName, Is.EqualTo("message"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Info_WhenMessageIsBlank_ThrowsArgumentException(string message)
        {
            GameLog.Configure(new RecordingLogSink(), LogLevel.Info);

            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => GameLog.Info(LogChannel.Boot, message))!;

            Assert.That(exception.ParamName, Is.EqualTo("message"));
        }

        [Test]
        public void Error_WhenExceptionProvided_AppendsExceptionDetails()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);

            GameLog.Error(LogChannel.Config, "配置缺失：CARD_001", new InvalidOperationException("缺少字段 cost"));

            Assert.That(sink.Entries[0].Message, Does.Contain("配置缺失：CARD_001"));
            Assert.That(sink.Entries[0].Message, Does.Contain("InvalidOperationException"));
            Assert.That(sink.Entries[0].Message, Does.Contain("缺少字段 cost"));
        }

        [Test]
        public void Error_WhenExceptionIsNull_KeepsMessageOnly()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);

            GameLog.Error(LogChannel.Config, "仅消息");

            Assert.That(sink.Entries[0].Message, Is.EqualTo("仅消息"));
        }

        [Test]
        public void Write_WhenSinkThrows_DoesNotPropagateAndRecordsFailure()
        {
            InvalidOperationException failure = new InvalidOperationException("sink 坏了");
            GameLog.Configure(new ThrowingLogSink(failure), LogLevel.Info);
            int before = GameLog.SinkFailureCount;

            Assert.DoesNotThrow(() => GameLog.Error(LogChannel.Boot, "不应穿透游戏循环"));

            Assert.That(GameLog.SinkFailureCount, Is.EqualTo(before + 1));
            Assert.That(GameLog.LastSinkFailure, Is.SameAs(failure));
        }

        [Test]
        public void Write_WhenSinkThrowsOnce_LaterLogsStillSucceed()
        {
            RecordingLogSink healthy = new RecordingLogSink();
            RecoveringSink recovering = new RecoveringSink(healthy);
            GameLog.Configure(recovering, LogLevel.Info);

            GameLog.Info(LogChannel.Boot, "第一次失败");
            GameLog.Info(LogChannel.Boot, "第二次成功");

            Assert.That(GameLog.SinkFailureCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(healthy.Entries.Count, Is.EqualTo(1));
            Assert.That(healthy.Entries[0].Message, Is.EqualTo("第二次成功"));
        }

        private sealed class RecoveringSink : ILogSink
        {
            private readonly RecordingLogSink _inner;
            private bool _failed;

            public RecoveringSink(RecordingLogSink inner)
            {
                _inner = inner;
            }

            public void Write(LogLevel level, LogChannel channel, string message)
            {
                if (!_failed)
                {
                    _failed = true;
                    throw new InvalidOperationException("第一次写入失败");
                }

                _inner.Write(level, channel, message);
            }
        }
    }
}
