using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>卡牌运动组件行为（M14-T2；Docs/05 §5.5 位置插值 + 进场动画）。</summary>
    [TestFixture]
    public sealed class CardMotionViewTests
    {
        private GameObject _root = null!;
        private RectTransform _rect = null!;
        private CanvasGroup _group = null!;
        private CardMotionView _motion = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CardMotion_Test", typeof(RectTransform), typeof(CanvasGroup));
            _rect = _root.GetComponent<RectTransform>();
            _group = _root.GetComponent<CanvasGroup>();
            _motion = _root.AddComponent<CardMotionView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void MoveTo_StartsTween_KeepsCurrentPosition()
        {
            _rect.anchoredPosition = new Vector2(100f, 0f);

            _motion.MoveTo(new Vector2(-100f, 0f));

            Assert.That(_motion.IsPlaying, Is.True);
            Assert.That(_rect.anchoredPosition.x, Is.EqualTo(100f).Within(0.001f), "补间起点是当前位置，不得瞬移");
        }

        [Test]
        public void MoveTo_Halfway_ProgressesPosition()
        {
            _rect.anchoredPosition = new Vector2(100f, 0f);

            _motion.MoveTo(new Vector2(-100f, 0f));
            _motion.Tick(CardMotionView.MoveDuration * 0.5f);

            Assert.That(_rect.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void MoveTo_Completes_ReachesTargetAndStops()
        {
            _rect.anchoredPosition = new Vector2(100f, 0f);

            _motion.MoveTo(new Vector2(-100f, 0f));
            _motion.Tick(CardMotionView.MoveDuration);

            Assert.That(_rect.anchoredPosition.x, Is.EqualTo(-100f).Within(0.001f));
            Assert.That(_motion.IsPlaying, Is.False);
        }

        [Test]
        public void MoveTo_SameTarget_DoesNotPlay()
        {
            _rect.anchoredPosition = new Vector2(50f, 0f);

            _motion.MoveTo(new Vector2(50f, 0f));

            Assert.That(_motion.IsPlaying, Is.False);
            Assert.That(_rect.anchoredPosition.x, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void MoveTo_RetargetMidTween_NoJump()
        {
            _rect.anchoredPosition = Vector2.zero;

            _motion.MoveTo(new Vector2(100f, 0f));
            _motion.Tick(CardMotionView.MoveDuration * 0.5f);
            float before = _rect.anchoredPosition.x;
            _motion.MoveTo(new Vector2(60f, 0f));

            Assert.That(_rect.anchoredPosition.x, Is.EqualTo(before).Within(0.01f), "重定目标瞬间不得跳变");
        }

        [Test]
        public void BeginEntry_PlacesAtTarget_InvisibleAndScaled()
        {
            _motion.BeginEntry(new Vector2(70f, 0f));

            Assert.That(_rect.anchoredPosition.x, Is.EqualTo(70f).Within(0.001f), "进场位置即落槽");
            Assert.That(_group.alpha, Is.EqualTo(0f));
            Assert.That(_root.transform.localScale.x, Is.EqualTo(CardMotionView.EntryScaleFrom).Within(0.001f));
            Assert.That(_motion.IsPlaying, Is.True);
        }

        [Test]
        public void BeginEntry_Completes_FullyVisibleNormalScale()
        {
            _motion.BeginEntry(new Vector2(70f, 0f));

            _motion.Tick(CardMotionView.EntryDuration);

            Assert.That(_group.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_motion.IsPlaying, Is.False);
        }

        [Test]
        public void MoveTo_DuringEntry_RestoresAlphaAndScale()
        {
            _motion.BeginEntry(Vector2.zero);
            _motion.Tick(CardMotionView.EntryDuration * 0.5f);

            _motion.MoveTo(new Vector2(30f, 0f));

            Assert.That(_group.alpha, Is.EqualTo(1f), "滑位必须覆盖入场残留的半透明");
            Assert.That(_root.transform.localScale, Is.EqualTo(Vector3.one), "滑位必须覆盖入场残留的缩放");
        }

        [Test]
        public void Stop_ResetsVisualState()
        {
            _motion.BeginEntry(Vector2.zero);
            _motion.Tick(CardMotionView.EntryDuration * 0.4f);

            _motion.Stop();

            Assert.That(_motion.IsPlaying, Is.False);
            Assert.That(_group.alpha, Is.EqualTo(1f));
            Assert.That(_root.transform.localScale, Is.EqualTo(Vector3.one));
        }
    }
}
