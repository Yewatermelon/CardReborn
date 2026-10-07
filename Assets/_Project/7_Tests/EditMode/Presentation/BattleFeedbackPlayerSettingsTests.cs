using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>反馈开关与加速语义（M5-T6；FR-8.2）：四类开关独立屏蔽，Speed 缩放全部时长。</summary>
    [TestFixture]
    internal sealed class BattleFeedbackPlayerSettingsTests
    {
        private FloatingTextView _prefab = null!;
        private Transform _poolRoot = null!;
        private Transform _textParent = null!;
        private FloatingTextPool _pool = null!;
        private TurnBannerView _banner = null!;
        private StubFeedbackTargetLocator _locator = null!;
        private StubAudioCuePlayer _audio = null!;
        private FeedbackSettings _settings = null!;
        private BattleFeedbackPlayer _player = null!;
        private CardView? _extraView;

        [SetUp]
        public void SetUp()
        {
            _prefab = FeedbackTestPrefabs.CreateFloatingTextView();
            _poolRoot = new GameObject("PoolRoot").transform;
            _textParent = new GameObject("TextParent", typeof(RectTransform)).transform;
            _pool = new FloatingTextPool(_prefab, _poolRoot, 2);
            _banner = FeedbackTestPrefabs.CreateTurnBannerView();
            _locator = new StubFeedbackTargetLocator();
            _audio = new StubAudioCuePlayer();
            _settings = new FeedbackSettings();
            _player = new BattleFeedbackPlayer(_locator, _pool, _textParent, _banner, _audio, _settings, 0);
            _extraView = null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_prefab.gameObject);
            Object.DestroyImmediate(_poolRoot.gameObject);
            Object.DestroyImmediate(_textParent.gameObject);
            Object.DestroyImmediate(_banner.gameObject);
            if (_extraView != null)
            {
                Object.DestroyImmediate(_extraView.gameObject);
            }
        }

        [Test]
        public void DamageNumbersDisabled_SkipsTextButKeepsCue()
        {
            _settings.DamageNumbersEnabled = false;
            _locator.MinionAnchors[7] = Vector3.zero;

            _player.Handle(new DamageEvent(null, 7, null, 3, false));

            Assert.AreEqual(0, _textParent.childCount);
            AssertCue(AudioCue.DamageDealt);
        }

        [Test]
        public void DeathFadeDisabled_SkipsFadeButKeepsCue()
        {
            _settings.DeathFadeEnabled = false;
            _extraView = Object.Instantiate(PresentationTestPrefabs.CreateCardViewPrefab());
            _locator.MinionViews[9] = _extraView;

            _player.Handle(new CardDeathEvent(9));

            CardFadeOutView fade = _extraView.GetComponent<CardFadeOutView>();
            Assert.IsTrue(fade == null || !fade.IsPlaying);
            AssertCue(AudioCue.MinionDeath);
        }

        [Test]
        public void TurnBannerDisabled_SkipsBannerButKeepsCue()
        {
            _settings.TurnBannerEnabled = false;

            _player.Handle(new TurnStartedEvent(2, 0));

            Assert.IsFalse(_banner.IsPlaying);
            AssertCue(AudioCue.TurnStarted);
        }

        [Test]
        public void AudioDisabled_SkipsCueButKeepsVisual()
        {
            _settings.AudioEnabled = false;
            _locator.MinionAnchors[7] = Vector3.zero;

            _player.Handle(new DamageEvent(null, 7, null, 3, false));

            Assert.AreEqual(1, _textParent.childCount);
            Assert.AreEqual(0, _audio.Played.Count);
        }

        [Test]
        public void SpeedDouble_HalvesFloatingTextDuration()
        {
            _settings.Speed = 2f;
            _locator.MinionAnchors[7] = Vector3.zero;

            _player.Handle(new DamageEvent(null, 7, null, 3, false));

            FloatingTextView text = _textParent.GetChild(0).GetComponent<FloatingTextView>();
            Assert.AreEqual(BattleFeedbackPlayer.FloatingTextDuration / 2f, text.Duration, 1e-6f);
        }

        [Test]
        public void SpeedDouble_HalvesBannerDuration()
        {
            _settings.Speed = 2f;

            _player.Handle(new TurnStartedEvent(2, 0));

            Assert.AreEqual(BattleFeedbackPlayer.BannerDuration / 2f, _banner.Duration, 1e-6f);
        }

        [Test]
        public void SpeedDouble_HalvesFadeDuration()
        {
            _settings.Speed = 2f;
            _extraView = Object.Instantiate(PresentationTestPrefabs.CreateCardViewPrefab());
            _locator.MinionViews[9] = _extraView;

            _player.Handle(new CardDeathEvent(9));

            CardFadeOutView fade = _extraView.GetComponent<CardFadeOutView>();
            Assert.AreEqual(BattleFeedbackPlayer.DeathFadeDuration / 2f, fade.Duration, 1e-6f);
        }

        private void AssertCue(AudioCue expected)
        {
            CollectionAssert.AreEqual(new[] { expected }, _audio.Played);
        }
    }
}
