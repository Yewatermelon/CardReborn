using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T5：把 M1-T2 的事件派发失败接到 GameLog（端到端闭合）。</summary>
    public sealed class EventDispatchLogSinkTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLogTestHelper.Reset();
        }

        [Test]
        public void OnHandlerFailed_WhenCalled_WritesErrorLogWithEventAndHandler()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            EventDispatchLogSink bridge = new EventDispatchLogSink();
            InvalidOperationException failure = new InvalidOperationException("bomb");

            bridge.OnHandlerFailed(new EventDispatchFailure(typeof(TestDamageEvent), "OnDamage", failure));

            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Match));
            Assert.That(sink.Entries[0].Message, Does.Contain("TestDamageEvent"));
            Assert.That(sink.Entries[0].Message, Does.Contain("OnDamage"));
            Assert.That(sink.Entries[0].Message, Does.Contain("bomb"));
        }

        [Test]
        public void OnHandlerFailed_WhenCustomChannelConfigured_UsesThatChannel()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            EventDispatchLogSink bridge = new EventDispatchLogSink(LogChannel.Ai);

            bridge.OnHandlerFailed(
                new EventDispatchFailure(typeof(TestTurnEvent), "OnTurn", new Exception("x")));

            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Ai));
        }

        [Test]
        public void Ctor_WhenChannelIsUndefined_ThrowsArgumentOutOfRangeException()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new EventDispatchLogSink((LogChannel)42));

            Assert.That(exception.ParamName, Is.EqualTo("channel"));
        }

        [Test]
        public void EndToEnd_WhenSubscriberThrows_FailureIsLoggedAndOtherSubscribersRun()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            EventBus bus = new EventBus(new EventDispatchLogSink());
            int survivorCalls = 0;
            bus.Subscribe<TestDamageEvent>(evt => throw new InvalidOperationException("subscriber boom"));
            bus.Subscribe<TestDamageEvent>(evt => survivorCalls++);

            PublishReport report = bus.Publish(new TestDamageEvent(3));

            Assert.That(survivorCalls, Is.EqualTo(1));
            Assert.That(report.FailureCount, Is.EqualTo(1));
            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(sink.Entries[0].Message, Does.Contain("TestDamageEvent"));
        }

        [Test]
        public void EndToEnd_WhenGameLogDisabled_FailureIsStillIsolatedWithoutLog()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            GameLog.Disable();
            EventBus bus = new EventBus(new EventDispatchLogSink());
            bus.Subscribe<TestDamageEvent>(evt => throw new InvalidOperationException("boom"));

            PublishReport report = bus.Publish(new TestDamageEvent(1));

            Assert.That(report.FailureCount, Is.EqualTo(1));
            Assert.That(sink.Entries, Is.Empty);
        }
    }
}
