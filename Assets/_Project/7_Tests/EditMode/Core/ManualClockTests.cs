using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T8：`ManualClock` 的推进、回拨与边界约束。</summary>
    public sealed class ManualClockTests
    {
        [Test]
        public void Ctor_WhenInitialTickOmitted_StartsAtZero()
        {
            ManualClock clock = new ManualClock();

            Assert.That(clock.CurrentTick, Is.EqualTo(0));
        }

        [Test]
        public void Ctor_WhenInitialTickProvided_ExposesIt()
        {
            ManualClock clock = new ManualClock(120);

            Assert.That(clock.CurrentTick, Is.EqualTo(120));
        }

        [Test]
        public void Ctor_WhenInitialTickIsNegative_ThrowsArgumentOutOfRangeException()
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => new ManualClock(-1))!;

            Assert.That(exception.ParamName, Is.EqualTo("initialTick"));
        }

        [Test]
        public void Advance_WhenCalled_IncrementsCurrentTick()
        {
            ManualClock clock = new ManualClock(10);

            clock.Advance(5);

            Assert.That(clock.CurrentTick, Is.EqualTo(15));
        }

        [Test]
        public void Advance_WhenZero_KeepsCurrentTick()
        {
            ManualClock clock = new ManualClock(7);

            clock.Advance(0);

            Assert.That(clock.CurrentTick, Is.EqualTo(7));
        }

        [Test]
        public void Advance_WhenDeltaIsNegative_ThrowsArgumentOutOfRangeException()
        {
            ManualClock clock = new ManualClock(7);

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1))!;

            Assert.That(exception.ParamName, Is.EqualTo("ticks"));
            Assert.That(exception.Message, Does.Contain("SetTo"), "错误信息应指路回拨用 SetTo");
            Assert.That(clock.CurrentTick, Is.EqualTo(7));
        }

        [Test]
        public void Advance_WhenCalledRepeatedly_Accumulates()
        {
            ManualClock clock = new ManualClock();

            for (int i = 0; i < 10; i++)
            {
                clock.Advance(1);
            }

            clock.Advance(3);

            Assert.That(clock.CurrentTick, Is.EqualTo(13));
        }

        [Test]
        public void Advance_WhenResultWouldOverflow_ThrowsInvalidOperationException()
        {
            ManualClock clock = new ManualClock(int.MaxValue - 1);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => clock.Advance(2))!;

            Assert.That(exception.Message, Does.Contain("溢出"));
            Assert.That(clock.CurrentTick, Is.EqualTo(int.MaxValue - 1), "溢出时不得改动状态");
        }

        [Test]
        public void Advance_WhenReachingMaxValueExactly_Succeeds()
        {
            ManualClock clock = new ManualClock(int.MaxValue - 1);

            clock.Advance(1);

            Assert.That(clock.CurrentTick, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void SetTo_WhenForward_SetsAbsoluteTick()
        {
            ManualClock clock = new ManualClock(10);

            clock.SetTo(250);

            Assert.That(clock.CurrentTick, Is.EqualTo(250));
        }

        [Test]
        public void SetTo_WhenBackward_SetsAbsoluteTick()
        {
            ManualClock clock = new ManualClock(250);

            clock.SetTo(10);

            Assert.That(clock.CurrentTick, Is.EqualTo(10), "回拨用于测试与重放");
        }

        [Test]
        public void SetTo_WhenTickIsNegative_ThrowsArgumentOutOfRangeException()
        {
            ManualClock clock = new ManualClock(10);

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetTo(-5))!;

            Assert.That(exception.ParamName, Is.EqualTo("tick"));
            Assert.That(clock.CurrentTick, Is.EqualTo(10));
        }

        [Test]
        public void Clock_WhenUsedThroughInterface_ExposesCurrentTick()
        {
            IClock clock = new ManualClock(33);

            Assert.That(clock.CurrentTick, Is.EqualTo(33));
        }

        [Test]
        public void Clocks_WhenTwoInstances_AreIndependent()
        {
            ManualClock first = new ManualClock(1);
            ManualClock second = new ManualClock(1);

            first.Advance(9);

            Assert.That(first.CurrentTick, Is.EqualTo(10));
            Assert.That(second.CurrentTick, Is.EqualTo(1));
        }
    }
}
