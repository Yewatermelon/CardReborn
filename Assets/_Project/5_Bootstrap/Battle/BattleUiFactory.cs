using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Log;
using Card.Presentation.Battle.Targeting;
using static Card.Bootstrap.Battle.BattleUiPrimitive;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 战斗 UI 代码工厂（M6-T1）：在空场景上构建 Canvas/EventSystem 与全部战斗视图，
    /// 不依赖手摆预制件与场景 YAML 引用（美术化布局属 M9）。
    /// </summary>
    public static class BattleUiFactory
    {
        private const int ReferenceWidth = 1920;
        private const int ReferenceHeight = 1080;

        public static BattleUi Create()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("BattleCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            var root = (RectTransform)canvasGo.transform;

            var ui = new BattleUi { CanvasRoot = root };
            CardView cardPrefab = BattleUiPrefabs.BuildCardPrefab();

            BuildHeroSide(ui, root);
            BuildBoardsAndHands(ui, root, cardPrefab);
            BuildButtons(ui, root);
            BuildTurnLabel(ui, root);
            BuildLog(ui, root);
            BuildBanner(ui, root);
            BuildFloatingTexts(ui, root);
            BuildArrow(ui, root);
            BuildErrorLayer(ui, root);
            BuildPassScreen(ui, root);
            BuildVictoryPanel(ui, root);
            BuildThinkingLabel(ui, root);

            return ui;
        }

        private static void EnsureEventSystem()
        {
            UiEventSystem.Ensure();
        }

        private static void BuildHeroSide(BattleUi ui, RectTransform root)
        {
            ui.EnemyHeroAnchor = CreateBox("EnemyHero", root, new Vector2(-800f, 430f), new Vector2(260f, 100f));
            ui.LocalHeroAnchor = CreateBox("LocalHero", root, new Vector2(-800f, -430f), new Vector2(260f, 100f));
            ui.EnemyHero = BuildHero(ui.EnemyHeroAnchor);
            ui.LocalHero = BuildHero(ui.LocalHeroAnchor);
            ui.EnemyMana = BuildMana(ui.EnemyHeroAnchor, new Vector2(150f, -34f));
            ui.LocalMana = BuildMana(ui.LocalHeroAnchor, new Vector2(150f, -34f));
        }

        private static HeroView BuildHero(RectTransform anchor)
        {
            var view = anchor.gameObject.AddComponent<HeroView>();
            Panel(anchor, "Portrait", new Vector2(-95f, 0f), new Vector2(80f, 80f));
            view._nameText = Text(anchor, "Name", new Vector2(20f, -20f), new Vector2(160f, 26f), 18);
            view._healthText = Text(anchor, "Health", new Vector2(-30f, 18f), new Vector2(120f, 34f), 24);
            var armorPanel = Panel(anchor, "ArmorPanel", new Vector2(90f, 18f), new Vector2(70f, 34f));
            view._armorPanel = armorPanel;
            view._armorText = Text(armorPanel.transform, "Armor", Vector2.zero, new Vector2(70f, 34f), 18);
            armorPanel.SetActive(false);
            return view;
        }

        private static ManaView BuildMana(RectTransform parent, Vector2 position)
        {
            var go = new GameObject("Mana", typeof(RectTransform));
            Place(go, parent, position, new Vector2(100f, 28f));
            var view = go.AddComponent<ManaView>();
            view._currentText = Text(go.transform, "Current", new Vector2(-20f, 0f), new Vector2(40f, 28f), 18);
            view._maxText = Text(go.transform, "Max", new Vector2(28f, 0f), new Vector2(40f, 28f), 18);
            return view;
        }

        private static void BuildBoardsAndHands(BattleUi ui, RectTransform root, CardView cardPrefab)
        {
            RectTransform enemyHand = CreateBox("EnemyHand", root, new Vector2(0f, 480f), new Vector2(1200f, 156f));
            RectTransform enemyBoard = CreateBox("EnemyBoard", root, new Vector2(0f, 250f), new Vector2(1200f, 156f));
            RectTransform localBoard = CreateBox("LocalBoard", root, new Vector2(0f, -180f), new Vector2(1200f, 156f));
            RectTransform localHand = CreateBox("LocalHand", root, new Vector2(0f, -400f), new Vector2(1200f, 156f));

            ui.EnemyHand = AddCardView<HandView>(enemyHand, cardPrefab, spacing: 128f, prewarm: 10);
            ui.EnemyBoard = AddCardView<BoardView>(enemyBoard, cardPrefab, spacing: 128f, prewarm: 7);
            ui.LocalBoard = AddCardView<BoardView>(localBoard, cardPrefab, spacing: 128f, prewarm: 7);
            ui.LocalHand = AddCardView<HandView>(localHand, cardPrefab, spacing: 128f, prewarm: 4);
        }

        private static TView AddCardView<TView>(
            RectTransform host, CardView cardPrefab, float spacing, int prewarm)
            where TView : Component
        {
            var view = host.gameObject.AddComponent<TView>();
            switch (view)
            {
                case HandView hand:
                    hand._cardPrefab = cardPrefab;
                    hand._spacing = spacing;
                    hand._prewarmCount = prewarm;
                    break;
                case BoardView board:
                    board._cardPrefab = cardPrefab;
                    board._spacing = spacing;
                    board._prewarmCount = prewarm;
                    break;
            }

            return view;
        }

        private static void BuildButtons(BattleUi ui, RectTransform root)
        {
            RectTransform endTurn = CreateBox("EndTurnButton", root, new Vector2(820f, -430f), new Vector2(200f, 90f));
            ui.EndTurnButton = Button(endTurn, out ColorBlock _);
            ui.EndTurnLabel = Text(endTurn, "End回合", 22);

            RectTransform power = CreateBox("HeroPowerButton", root, new Vector2(-560f, -430f), new Vector2(90f, 90f));
            ui.HeroPowerButton = Button(power, out _);
            Text(power, "技能", 18);
        }

        private static void BuildTurnLabel(BattleUi ui, RectTransform root)
        {
            // 置于结束回合按钮上方，AC-4 需要"回合数 +1"的可见凭据。
            RectTransform box = CreateBox("TurnLabel", root, new Vector2(820f, -350f), new Vector2(200f, 40f));
            ui.TurnLabel = Text(box, "回合 1", 20);
        }

        private static void BuildLog(BattleUi ui, RectTransform root)
        {
            RectTransform panel = CreateBox("BattleLog", root, new Vector2(760f, 40f), new Vector2(320f, 620f));
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            Place(scrollGo, panel, Vector2.zero, Vector2.zero, stretch: true);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            RectTransform viewport = CreateBox("Viewport", scrollGo.transform, Vector2.zero, Vector2.zero);
            Stretch(viewport);
            var maskImage = viewport.gameObject.AddComponent<Mask>();
            maskImage.showMaskGraphic = false;
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            var entryGo = new GameObject("EntryPrefab", typeof(RectTransform));
            entryGo.transform.SetParent(panel, false);
            TMP_Text entryPrefab = entryGo.AddComponent<TextMeshProUGUI>();
            entryPrefab.fontSize = 13;
            entryGo.AddComponent<LayoutElement>().minHeight = 22f;
            entryGo.SetActive(false);

            var view = panel.gameObject.AddComponent<BattleLogView>();
            view.InitializeForTests(entryPrefab, content, scroll, maxEntries: 100);
            ui.Log = view;
        }

        private static void BuildBanner(BattleUi ui, RectTransform root)
        {
            RectTransform panel = CreateBox("TurnBanner", root, Vector2.zero, new Vector2(640f, 120f));
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            TMP_Text text = Text(panel, string.Empty, 34);
            var view = panel.gameObject.AddComponent<TurnBannerView>();
            view._text = text;
            view.Hide();
            ui.Banner = view;
        }

        private static void BuildFloatingTexts(BattleUi ui, RectTransform root)
        {
            RectTransform layer = CreateBox("FloatingTextLayer", root, Vector2.zero, Vector2.zero);
            Stretch(layer);
            ui.FloatingTextLayer = layer;
            var poolRoot = new GameObject("FloatingTextPoolRoot", typeof(RectTransform)).transform;
            poolRoot.SetParent(layer, false);
            poolRoot.gameObject.SetActive(false);
            FloatingTextView prefab = BattleUiPrefabs.BuildFloatingTextPrefab();
            ui.FloatingTextPool = new FloatingTextPool(prefab, poolRoot, prewarmCount: 4);
        }

        private static void BuildArrow(BattleUi ui, RectTransform root)
        {
            RectTransform area = CreateBox("TargetingArrow", root, Vector2.zero, Vector2.zero);
            Stretch(area);
            var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(area, false);
            var line = (RectTransform)lineGo.transform;
            line.sizeDelta = new Vector2(0f, 6f);
            lineGo.GetComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 0.9f);
            var view = area.gameObject.AddComponent<TargetingArrowView>();
            view._area = area;
            view._line = line;
            view._lineWidth = 6f;
            view.Initialize(null);
            view.Hide();
            ui.Arrow = view;
        }

        private static void BuildErrorLayer(BattleUi ui, RectTransform root)
        {
            RectTransform panel = CreateBox("ErrorPanel", root, Vector2.zero, new Vector2(1200f, 400f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.25f, 0f, 0f, 0.92f);
            TMP_Text text = Text(panel, string.Empty, 22);
            ui.ErrorPanel = panel.gameObject.GetComponent<CanvasGroup>()
                ?? panel.gameObject.AddComponent<CanvasGroup>();
            ui.ErrorText = text;
            panel.gameObject.SetActive(false);
        }

        private static void BuildPassScreen(BattleUi ui, RectTransform root)
        {
            RectTransform panel = CreateBox("PassScreen", root, Vector2.zero, Vector2.zero);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            TMP_Text text = Text(panel, string.Empty, 36);
            ui.PassScreen = panel.gameObject.AddComponent<CanvasGroup>();
            ui.PassScreenLabel = text;
            ui.PassScreenButton = Button(panel, out _);
            panel.gameObject.SetActive(false);
        }

        private static void BuildVictoryPanel(BattleUi ui, RectTransform root)
        {
            RectTransform panel = CreateBox("VictoryPanel", root, Vector2.zero, new Vector2(600f, 300f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.05f, 0.15f, 0.95f);
            TMP_Text label = Text(panel, string.Empty, 40);
            ui.VictoryPanel = panel.gameObject.AddComponent<CanvasGroup>();
            ui.VictoryLabel = label;

            RectTransform btn = CreateBox("PlayAgainButton", panel, new Vector2(0f, -80f), new Vector2(220f, 60f));
            ui.VictoryButton = Button(btn, out _);
            Text(btn, "再来一局", 22);
            panel.gameObject.SetActive(false);
        }

        private static void BuildThinkingLabel(BattleUi ui, RectTransform root)
        {
            // M7-OBS-1：人机 AI 回合思考提示，位于敌人英雄下方。
            RectTransform box = CreateBox("ThinkingLabel", root, new Vector2(-800f, 380f), new Vector2(300f, 40f));
            ui.ThinkingLabel = Text(box, "AI 思考中...", 20);
            ui.ThinkingLabel.color = new Color(0.85f, 0.75f, 0.45f, 1f);
            box.gameObject.SetActive(false);
        }

        private static RectTransform CreateBox(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place(go, parent, position, size);
            return (RectTransform)go.transform;
        }
    }
}
