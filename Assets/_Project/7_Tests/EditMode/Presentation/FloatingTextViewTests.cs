using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>浮动文字视图（M5-T6）：Show/Tick 的文本、颜色、渐隐与上浮。</summary>
    [TestFixture]
    internal sealed class FloatingTextViewTests
    {
        private FloatingTextView _view = null!;
        private RectTransform _rect = null!;

        [SetUp]
        public void SetUp()
        {
            _view = FeedbackTestPrefabs.CreateFloatingTextView();
            _rect = (RectTransform)_view.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view.gameObject);
        }

        [Test]
        public void Show_SetsTextColorAlphaAndActivates()
        {
            _view.Show("-3", Color.red, 1.2f);

            Assert.AreEqual("-3", _view._text.text);
            Assert.AreEqual(Color.red, _view._text.color);
            Assert.AreEqual(1f, _view._canvasGroup.alpha, 1e-6f);
            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.IsTrue(_view.IsPlaying);
            Assert.AreEqual(1.2f, _view.Duration, 1e-6f);
        }

        [Test]
        public void Tick_Halfway_FadesHalfAndRises()
        {
            _view.gameObject.SetActive(true);
            _rect.anchoredPosition = new Vector2(10f, 20f);
            _view.Show("-3", Color.red, 1.0f);

            _view.Tick(0.5f);

            Assert.AreEqual(0.5f, _view._canvasGroup.alpha, 1e-3f);
            Assert.AreEqual(20f + 60f * 0.5f, _rect.anchoredPosition.y, 1e-3f);
            Assert.IsTrue(_view.IsPlaying);
        }

        [Test]
        public void Tick_BeyondDuration_HidesAndStops()
        {
            _view.gameObject.SetActive(true);
            _view.Show("-3", Color.red, 1.0f);

            _view.Tick(1.0f);

            Assert.IsFalse(_view.IsPlaying);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Tick_WhenIdle_IsNoOp()
        {
            _view.Tick(0.5f);

            Assert.IsFalse(_view.IsPlaying);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Show_AfterFinished_ReArmsFromCurrentPosition()
        {
            _view.gameObject.SetActive(true);
            _rect.anchoredPosition = new Vector2(0f, 0f);
            _view.Show("-3", Color.red, 1.0f);
            _view.Tick(1.0f);

            _rect.anchoredPosition = new Vector2(50f, 50f);
            _view.Show("+2", Color.green, 0.6f);

            Assert.AreEqual("+2", _view._text.text);
            Assert.AreEqual(1f, _view._canvasGroup.alpha, 1e-6f);
            Assert.IsTrue(_view.IsPlaying);
            _view.Tick(0.3f);
            Assert.AreEqual(50f + 60f * 0.3f, _rect.anchoredPosition.y, 1e-3f);
        }
    }
}
