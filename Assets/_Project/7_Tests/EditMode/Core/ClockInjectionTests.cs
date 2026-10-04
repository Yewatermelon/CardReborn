using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T8：把假时钟注入消费者，验证"时间可精确控制"（不依赖真实时间）。</summary>
    public sealed class ClockInjectionTests
    {
        [Test]
        public void Consumer_WhenClockNotAdvanced_DoesNotExpire()
        {
            ManualClock clock = new ManualClock(100);
            TurnTimer timer = new TurnTimer(clock, limitTicks: 10);
            timer.StartTurn();

            Assert.That(timer.IsExpired, Is.False);
            Assert.That(timer.RemainingTicks, Is.EqualTo(10));
        }

        [Test]
        public void Consumer_WhenClockReachesLimit_ExpiresExactlyAtDeadline()
        {
            ManualClock clock = new ManualClock(100);
            TurnTimer timer = new TurnTimer(clock, limitTicks: 10);
            timer.StartTurn();

            clock.Advance(9);
            Assert.That(timer.IsExpired, Is.False, "差 1 tick 不应到期");

            clock.Advance(1);
            Assert.That(timer.IsExpired, Is.True, "到期判断不依赖真实时间，只看 tick");
            Assert.That(timer.RemainingTicks, Is.EqualTo(0));
        }

        [Test]
        public void Consumer_WhenSameSequenceRepeated_IsDeterministic()
        {
            Assert.That(RunSequence(), Is.EqualTo(RunSequence()), "同一 tick 序列必须得到同一结果");
        }

        [Test]
        public void Consumer_WhenClockRewound_RecoversRemainingTicks()
        {
            ManualClock clock = new ManualClock(100);
            TurnTimer timer = new TurnTimer(clock, limitTicks: 10);
            timer.StartTurn();
            clock.Advance(12);
            Assert.That(timer.IsExpired, Is.True);

            clock.SetTo(105);

            Assert.That(timer.IsExpired, Is.False, "回拨用于重放/恢复场景");
            Assert.That(timer.RemainingTicks, Is.EqualTo(5));
        }

        private static string RunSequence()
        {
            ManualClock clock = new ManualClock();
            TurnTimer timer = new TurnTimer(clock, limitTicks: 3);
            timer.StartTurn();
            string trace = string.Empty;

            for (int i = 0; i < 5; i++)
            {
                clock.Advance(1);
                trace += timer.IsExpired ? "T" : "F";
            }

            return trace;
        }

        /// <summary>测试用消费者：回合计时器，只依赖 IClock（真实实现由外层注入）。</summary>
        private sealed class TurnTimer
        {
            private readonly IClock _clock;
            private readonly int _limitTicks;
            private int _deadlineTick;

            public TurnTimer(IClock clock, int limitTicks)
            {
                _clock = clock;
                _limitTicks = limitTicks;
            }

            public int RemainingTicks
            {
                get { return Math.Max(0, _deadlineTick - _clock.CurrentTick); }
            }

            public bool IsExpired
            {
                get { return _clock.CurrentTick >= _deadlineTick; }
            }

            public void StartTurn()
            {
                _deadlineTick = _clock.CurrentTick + _limitTicks;
            }
        }
    }
}
