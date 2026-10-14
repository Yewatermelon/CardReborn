using System.Collections.Generic;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Log;
using Card.Presentation.Battle.Targeting;
using Card.Tests.EditMode.Match;
using Card.Tests.EditMode.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// M15-T1-B1：BattleViewSynchronizer 明牌视角测试（2 例）。
    /// observerMode=true 时对手手牌显示卡面（InstanceId 非 null）；
    /// 默认 false 时对手手牌显示牌背（InstanceId 为 null）——回归保护。
    /// </summary>
    [TestFixture]
    public sealed class BattleViewSynchronizerObserverTests
    {
        private GameObject _root = null!;
        private CardView _cardPrefab = null!;
        private BattleUi _ui = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ObserverSync_Test", typeof(RectTransform));
            _cardPrefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _cardPrefab.gameObject.SetActive(false);
            _ui = BuildMinimalBattleUi(_root, _cardPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_cardPrefab.gameObject);
        }

        [Test]
        public void ObserverMode_EnemyHandShowsCardFace()
        {
            CardDatabase db = RuleEngineTestHelpers.BuildDatabase();
            MatchState state = RuleEngineTestHelpers.BuildState();

            // 对手（座 1）手牌放 1 张卡（InstanceId=200）
            CardInstance enemyCard = CardInstance.FromDefinition(db.RequireCard("M1"), 200, 1);
            state.GetPlayer(1).Hand.Add(enemyCard);

            var sync = new BattleViewSynchronizer(_ui, db, KeyPassthroughTextResolver.Instance, 0, 1, observerMode: true);
            sync.Push(state);

            Assert.That(_ui.EnemyHand.ChildCount, Is.EqualTo(1), "observer 模式应显示对手 1 张手牌");
            Assert.That(_ui.EnemyHand.Children[0].InstanceId.HasValue, Is.True,
                "observer 模式对手手牌应显示卡面（InstanceId 非 null）");
            Assert.That(_ui.EnemyHand.Children[0].InstanceId!.Value, Is.EqualTo(200),
                "InstanceId 应为真实实例 200");
        }

        [Test]
        public void DefaultMode_EnemyHandShowsCardBack()
        {
            CardDatabase db = RuleEngineTestHelpers.BuildDatabase();
            MatchState state = RuleEngineTestHelpers.BuildState();

            CardInstance enemyCard = CardInstance.FromDefinition(db.RequireCard("M1"), 200, 1);
            state.GetPlayer(1).Hand.Add(enemyCard);

            // 不传 observerMode → 默认 false（产品对战裁剪行为不变）
            var sync = new BattleViewSynchronizer(_ui, db, KeyPassthroughTextResolver.Instance, 0, 1);
            sync.Push(state);

            Assert.That(_ui.EnemyHand.ChildCount, Is.EqualTo(1), "默认模式应显示对手手牌数量");
            Assert.That(_ui.EnemyHand.Children[0].InstanceId.HasValue, Is.False,
                "默认模式对手手牌应显示牌背（InstanceId 为 null）");
        }

        // ===== 夹具（与 HotSeatSynchronizerTests 同口径，复制以避免改动既有测试文件） =====

        private static BattleUi BuildMinimalBattleUi(GameObject root, CardView cardPrefab)
        {
            var ui = new BattleUi
            {
                CanvasRoot = (RectTransform)root.transform,
                LocalHand = CreateHand(root.transform, "LocalHand", cardPrefab),
                EnemyHand = CreateHand(root.transform, "EnemyHand", cardPrefab),
                LocalBoard = CreateBoard(root.transform, "LocalBoard", cardPrefab),
                EnemyBoard = CreateBoard(root.transform, "EnemyBoard", cardPrefab),
                LocalHero = CreateHero(root.transform, "LocalHero"),
                EnemyHero = CreateHero(root.transform, "EnemyHero"),
                LocalMana = CreateMana(root.transform, "LocalMana"),
                EnemyMana = CreateMana(root.transform, "EnemyMana"),
                LocalHeroAnchor = CreateRect(root.transform, "LocalHeroAnchor"),
                EnemyHeroAnchor = CreateRect(root.transform, "EnemyHeroAnchor"),
                EndTurnButton = CreateButton(root.transform, "EndTurnBtn"),
                HeroPowerButton = CreateButton(root.transform, "HeroPowerBtn"),
                EndTurnLabel = CreateText(root.transform),
                TurnLabel = CreateText(root.transform),
                Log = root.AddComponent<BattleLogView>(),
                Banner = root.AddComponent<TurnBannerView>(),
                FloatingTextLayer = CreateRect(root.transform, "FloatingLayer"),
                FloatingTextPool = null!,
                Arrow = root.AddComponent<TargetingArrowView>(),
            };
            return ui;
        }

        private static HandView CreateHand(Transform parent, string name, CardView prefab)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<HandView>();
            view._cardPrefab = prefab;
            view._spacing = 120f;
            view._prewarmCount = 0;
            return view;
        }

        private static BoardView CreateBoard(Transform parent, string name, CardView prefab)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view._cardPrefab = prefab;
            view._spacing = 140f;
            view._prewarmCount = 0;
            return view;
        }

        private static HeroView CreateHero(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<HeroView>();
            view._healthText = CreateText(go.transform);
            view._armorText = CreateText(go.transform);
            view._armorPanel = new GameObject("Armor") { transform = { parent = go.transform } };
            view._nameText = CreateText(go.transform);
            return view;
        }

        private static ManaView CreateMana(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ManaView>();
            view._currentText = CreateText(go.transform);
            view._maxText = CreateText(go.transform);
            return view;
        }

        private static RectTransform CreateRect(Transform parent, string name = "Rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static UnityEngine.UI.Button CreateButton(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<UnityEngine.UI.Button>();
        }

        private static TMPro.TMP_Text CreateText(Transform parent)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<TMPro.TextMeshProUGUI>();
        }
    }
}
