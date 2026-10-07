using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>死亡淡出视图（M5-T6）：CanvasGroup alpha 从 1 渐隐到 0，可中止，缺组件自动补。</summary>
    [TestFixture]
    internal sealed class CardFadeOutViewTests
    {
        private GameObject _go = null!;
        private CardFadeOutView _fade = null!;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("FadeTarget", typeof(RectTransform));
            _go.AddComponent<CanvasGroup>();
            _fade = _go.AddComponent<CardFadeOutView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Play_FadesAlphaToZeroOverDuration()
        {
            CanvasGroup group = _go.GetComponent<CanvasGroup>();

            _fade.Play(1.0f);
            Assert.IsTrue(_fade.IsPlaying);
            Assert.AreEqual(1f, group.alpha, 1e-6f);

            _fade.Tick(0.5f);
            Assert.AreEqual(0.5f, group.alpha, 1e-3f);

            _fade.Tick(0.5f);
            Assert.AreEqual(0f, group.alpha, 1e-3f);
            Assert.IsFalse(_fade.IsPlaying);
        }

        [Test]
        public void Stop_HaltsKeepingCurrentAlpha()
        {
            CanvasGroup group = _go.GetComponent<CanvasGroup>();
            _fade.Play(1.0f);
            _fade.Tick(0.5f);

            _fade.Stop();
            _fade.Tick(0.5f);

            Assert.IsFalse(_fade.IsPlaying);
            Assert.AreEqual(0.5f, group.alpha, 1e-3f);
        }

        [Test]
        public void Play_AddsCanvasGroupWhenMissing()
        {
            var bare = new GameObject("Bare", typeof(RectTransform));
            try
            {
                var fade = bare.AddComponent<CardFadeOutView>();

                fade.Play(1.0f);

                Assert.IsNotNull(bare.GetComponent<CanvasGroup>());
                Assert.AreEqual(1f, bare.GetComponent<CanvasGroup>().alpha, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }
    }
}
