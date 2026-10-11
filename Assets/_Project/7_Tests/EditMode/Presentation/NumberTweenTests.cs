using Card.Presentation.Battle.Feedback;
using NUnit.Framework;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>数值补间单元行为（M14-T2；Docs/05 §5.5 数值 Tween）。</summary>
    [TestFixture]
    public sealed class NumberTweenTests
    {
        private const float Duration = 0.5f;

        [Test]
        public void FirstTo_SnapsImmediately_DoesNotTweenFromZero()
        {
            var tween = new NumberTween(Duration);

            tween.To(30);

            Assert.That(tween.Current, Is.EqualTo(30), "首次赋值必须立即到位，不得从 0 起播");
            Assert.That(tween.IsPlaying, Is.False);
            Assert.That(tween.HasValue, Is.True);
        }

        [Test]
        public void To_SameValue_DoesNotPlay()
        {
            var tween = new NumberTween(Duration);

            tween.To(10);
            tween.To(10);

            Assert.That(tween.IsPlaying, Is.False);
        }

        [Test]
        public void To_AfterSnap_StartsTweenKeepingCurrent()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(20);
            tween.To(12);

            Assert.That(tween.IsPlaying, Is.True, "目标变化应开始补间");
            Assert.That(tween.Current, Is.EqualTo(20), "补间开始瞬间不得跳变到目标");
        }

        [Test]
        public void Tick_Halfway_ProgressesLinearly()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(20);
            tween.To(10);
            tween.Tick(Duration * 0.5f);

            Assert.That(tween.Current, Is.EqualTo(15));
            Assert.That(tween.IsPlaying, Is.True);
        }

        [Test]
        public void Tick_PastDuration_ReachesTargetAndStops()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(20);
            tween.To(10);
            tween.Tick(Duration);
            tween.Tick(Duration);

            Assert.That(tween.Current, Is.EqualTo(10));
            Assert.That(tween.IsPlaying, Is.False);
        }

        [Test]
        public void Tick_MidTween_RetargetContinuesFromCurrent()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(0);
            tween.To(100);
            tween.Tick(Duration * 0.5f);
            tween.To(60);
            tween.Tick(Duration);

            Assert.That(tween.Current, Is.EqualTo(60));
            Assert.That(tween.IsPlaying, Is.False);
        }

        [Test]
        public void Tick_MidTween_RetargetDoesNotJump()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(0);
            tween.To(100);
            tween.Tick(Duration * 0.5f);
            tween.To(60);

            Assert.That(tween.Current, Is.EqualTo(50), "重定目标瞬间必须保持当前显示值");
        }

        [Test]
        public void Snap_CancelsPlayingTween()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(0);
            tween.To(100);
            tween.Tick(0.1f);
            tween.Snap(7);

            Assert.That(tween.Current, Is.EqualTo(7));
            Assert.That(tween.IsPlaying, Is.False);
        }

        [Test]
        public void Tick_WhenNotPlaying_NoOp()
        {
            var tween = new NumberTween(Duration);

            tween.Snap(5);
            tween.Tick(10f);

            Assert.That(tween.Current, Is.EqualTo(5));
        }
    }
}
