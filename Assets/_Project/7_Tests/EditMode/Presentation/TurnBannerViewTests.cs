using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>回合横幅视图（M5-T6）：显示、到时自隐、立即隐藏、重播重计时。</summary>
    [TestFixture]
    internal sealed class TurnBannerViewTests
    {
        private TurnBannerView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _view = FeedbackTestPrefabs.CreateTurnBannerView();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view.gameObject);
        }

        [Test]
        public void Show_DisplaysTextAndActivates()
        {
            _view.Show("你的回合", 1.6f);

            Assert.AreEqual("你的回合", _view._text.text);
            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.IsTrue(_view.IsPlaying);
            Assert.AreEqual(1.6f, _view.Duration, 1e-6f);
        }

        [Test]
        public void Tick_PastDuration_AutoHides()
        {
            _view.Show("你的回合", 1.0f);

            _view.Tick(1.0f);

            Assert.IsFalse(_view.IsPlaying);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Hide_DismissesImmediately()
        {
            _view.Show("你的回合", 10f);

            _view.Hide();

            Assert.IsFalse(_view.IsPlaying);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Show_WhilePlaying_RestartsTimerAndText()
        {
            _view.Show("你的回合", 1.0f);
            _view.Tick(0.9f);

            _view.Show("对手回合", 1.0f);
            _view.Tick(0.9f);

            Assert.AreEqual("对手回合", _view._text.text);
            Assert.IsTrue(_view.IsPlaying);

            _view.Tick(0.2f);

            Assert.IsFalse(_view.IsPlaying);
        }
    }
}
