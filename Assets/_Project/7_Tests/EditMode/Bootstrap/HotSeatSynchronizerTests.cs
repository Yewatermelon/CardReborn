using System.Collections.Generic;
using System.Linq;
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
    /// M6-T4（B7 查证）：热座切换后 Synchronizer 推送的卡牌分布是否正确。
    /// 若此测试通过但用户实机仍观察到错位，根因在事件流或别处而非 Synchronizer。
    /// </summary>
    [TestFixture]
    public sealed class HotSeatSynchronizerTests
    {
        private GameObject _root = null!;
        private CardView _cardPrefab = null!;
        private BattleUi _ui = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HotSeatSync_Test", typeof(RectTransform));
            _cardPrefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _cardPrefab.gameObject.SetActive(false);
            _ui = BuildMinimalBattleUi(_root, _cardPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_cardPrefab.gameObject);
        }

        [Test]
        public void Push_AfterSwitchSeats_SwapsLocalAndEnemyBoardContents()
        {
            CardDatabase db = RuleEngineTestHelpers.BuildDatabase();
            MatchState state = RuleEngineTestHelpers.BuildState();

            // p0 棋盘放 1 张，p1 棋盘放 3 张（用 M1/2/4 不同卡避免去重）。
            CardInstance p0m1 = CardInstance.FromDefinition(db.RequireCard("M1"), 100, 0);
            state.GetPlayer(0).Board.Add(p0m1);

            CardInstance p1m1 = CardInstance.FromDefinition(db.RequireCard("M2_TAUNT"), 200, 1);
            CardInstance p1m2 = CardInstance.FromDefinition(db.RequireCard("M4_STEALTH"), 201, 1);
            CardInstance p1m3 = CardInstance.FromDefinition(db.RequireCard("M3_CHARGE"), 202, 1);
            state.GetPlayer(1).Board.Add(p1m1);
            state.GetPlayer(1).Board.Add(p1m2);
            state.GetPlayer(1).Board.Add(p1m3);

            var sync = new BattleViewSynchronizer(_ui, db, KeyPassthroughTextResolver.Instance, 0, 1);

            // 初始视角（座 0 为本地）。
            sync.Push(state);
            Assert.That(_ui.LocalBoard.ChildCount, Is.EqualTo(1), "切换前 LocalBoard 应显示座 0 的 1 张");
            Assert.That(_ui.EnemyBoard.ChildCount, Is.EqualTo(3), "切换前 EnemyBoard 应显示座 1 的 3 张");

            // 切换视角。
            sync.SwitchSeats();
            sync.Push(state);

            Assert.That(_ui.LocalBoard.ChildCount, Is.EqualTo(3), "切换后 LocalBoard 应显示座 1 的 3 张");
            Assert.That(_ui.EnemyBoard.ChildCount, Is.EqualTo(1), "切换后 EnemyBoard 应显示座 0 的 1 张");
        }

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
