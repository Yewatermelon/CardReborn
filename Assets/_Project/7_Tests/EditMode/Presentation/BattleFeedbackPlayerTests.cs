using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>反馈播放器事件映射（M5-T6）：12 类 GameEvent → 浮动数字/淡出/横幅/音效钩子。</summary>
    [TestFixture]
    internal sealed class BattleFeedbackPlayerTests
    {
        private const int LocalSeat = 0;

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
            _player = new BattleFeedbackPlayer(_locator, _pool, _textParent, _banner, _audio, _settings, LocalSeat);
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
        public void Damage_OnMinion_FloatsRedNumberAtMinionAnchor()
        {
            _locator.MinionAnchors[7] = new Vector3(100f, 50f, 0f);

            _player.Handle(new DamageEvent(null, 7, null, 3, false));

            FloatingTextView text = OnlyText();
            Assert.AreEqual("-3", text._text.text);
            Assert.Greater(text._text.color.r, text._text.color.g);
            Assert.AreEqual(new Vector3(100f, 50f, 0f), text.transform.position);
            AssertCue(AudioCue.DamageDealt);
        }

        [Test]
        public void Damage_OnHero_FloatsAtHeroAnchor()
        {
            _locator.HeroAnchors[1] = new Vector3(-20f, 5f, 0f);

            _player.Handle(new DamageEvent(null, null, 1, 2, false));

            FloatingTextView text = OnlyText();
            Assert.AreEqual("-2", text._text.text);
            Assert.AreEqual(new Vector3(-20f, 5f, 0f), text.transform.position);
            AssertCue(AudioCue.DamageDealt);
        }

        [Test]
        public void Damage_LocatorMiss_NoTextButCueStillPlays()
        {
            _player.Handle(new DamageEvent(null, 999, null, 3, false));

            Assert.AreEqual(0, _textParent.childCount);
            AssertCue(AudioCue.DamageDealt);
        }

        [Test]
        public void Healing_FloatsGreenPlusNumber()
        {
            _locator.MinionAnchors[7] = Vector3.zero;

            _player.Handle(new HealingEvent(7, null, 4));

            FloatingTextView text = OnlyText();
            Assert.AreEqual("+4", text._text.text);
            Assert.Greater(text._text.color.g, text._text.color.r);
            AssertCue(AudioCue.HealingReceived);
        }

        [Test]
        public void Fatigue_FloatsOnThatHeroWithCue()
        {
            _locator.HeroAnchors[1] = Vector3.zero;

            _player.Handle(new FatigueEvent(1, 2, 2));

            FloatingTextView text = OnlyText();
            Assert.AreEqual("-2", text._text.text);
            AssertCue(AudioCue.FatigueDamage);
        }

        [Test]
        public void Death_StartsFadeOutOnLocatedView()
        {
            _extraView = Object.Instantiate(PresentationTestPrefabs.CreateCardViewPrefab());
            _locator.MinionViews[9] = _extraView;

            _player.Handle(new CardDeathEvent(9));

            CardFadeOutView fade = _extraView.GetComponent<CardFadeOutView>();
            Assert.IsNotNull(fade);
            Assert.IsTrue(fade.IsPlaying);
            AssertCue(AudioCue.MinionDeath);
        }

        [Test]
        public void Death_LocatorMiss_NoFadeButCueStillPlays()
        {
            _player.Handle(new CardDeathEvent(999));

            AssertCue(AudioCue.MinionDeath);
        }

        [Test]
        public void TurnStarted_LocalSeat_ShowsYourTurnBanner()
        {
            _player.Handle(new TurnStartedEvent(2, LocalSeat));

            Assert.AreEqual("你的回合", _banner._text.text);
            Assert.IsTrue(_banner.IsPlaying);
            AssertCue(AudioCue.TurnStarted);
        }

        [Test]
        public void TurnStarted_EnemySeat_ShowsEnemyTurnBanner()
        {
            _player.Handle(new TurnStartedEvent(2, 1));

            Assert.AreEqual("对手回合", _banner._text.text);
            AssertCue(AudioCue.TurnStarted);
        }

        [TestCase(MatchResult.Player0Wins, 0, "胜利！", AudioCue.Victory)]
        [TestCase(MatchResult.Player1Wins, 1, "败北", AudioCue.Defeat)]
        [TestCase(MatchResult.Draw, null, "平局", AudioCue.MatchDraw)]
        public void MatchEnded_MapsResultToBannerAndCue(MatchResult result, int? winner, string text, AudioCue cue)
        {
            _player.Handle(new MatchEndedEvent(result, winner, 5, "r"));

            Assert.AreEqual(text, _banner._text.text);
            Assert.IsTrue(_banner.IsPlaying);
            AssertCue(cue);
        }

        [Test]
        public void SimpleEvents_EmitCuesWithoutVisuals()
        {
            _player.Handle(new CardPlayedEvent(0, 11));
            _player.Handle(new AttackDeclaredEvent(11, 12, null));
            _player.Handle(new CardDrawnEvent(0, 13));
            _player.Handle(new CardBurnedEvent(0, 14));

            Assert.AreEqual(0, _textParent.childCount);
            Assert.IsFalse(_banner.IsPlaying);
            CollectionAssert.AreEqual(
                new[] { AudioCue.CardPlayed, AudioCue.AttackDeclared, AudioCue.CardDrawn, AudioCue.CardBurned },
                _audio.Played);
        }

        [Test]
        public void PumpIntegration_DispatchesAppendedEvents()
        {
            var log = new EventLog();
            log.Emit(new CardPlayedEvent(0, 1));
            var pump = new MatchEventPump(log.Events);
            _player.Bind(pump);
            _locator.MinionAnchors[7] = Vector3.zero;
            log.Emit(new DamageEvent(null, 7, null, 3, false));

            int pumped = pump.Pump();

            Assert.AreEqual(1, pumped);
            Assert.AreEqual(1, _textParent.childCount);
            AssertCue(AudioCue.DamageDealt);
        }

        [Test]
        public void Unbind_StopsFeedback()
        {
            var log = new EventLog();
            var pump = new MatchEventPump(log.Events);
            _player.Bind(pump);
            _player.Unbind();
            log.Emit(new CardPlayedEvent(0, 1));

            pump.Pump();

            Assert.AreEqual(0, _audio.Played.Count);
        }

        private FloatingTextView OnlyText()
        {
            Assert.AreEqual(1, _textParent.childCount);
            return _textParent.GetChild(0).GetComponent<FloatingTextView>();
        }

        private void AssertCue(AudioCue expected)
        {
            CollectionAssert.AreEqual(new[] { expected }, _audio.Played);
        }
    }
}
