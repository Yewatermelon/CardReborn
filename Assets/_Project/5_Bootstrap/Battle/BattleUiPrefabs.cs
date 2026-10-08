using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 运行时预制件构建（M6-T1）：不依赖任何 .prefab 资源，用代码生成
    /// <see cref="CardView"/> 与浮动数字预制件（灰板功能级布局，美术属 M9）。
    /// </summary>
    public static class BattleUiPrefabs
    {
        public static readonly Color CardBackColor = new Color(0.16f, 0.16f, 0.22f, 0.98f);
        public static readonly Color TextColor = Color.white;
        public static readonly Color TauntColor = new Color(0.95f, 0.75f, 0.2f, 0.55f);

        /// <summary>构建未激活的卡牌预制件（调用方 Instantiate 使用）。</summary>
        public static CardView BuildCardPrefab()
        {
            var go = new GameObject("CardPrefab", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(120f, 156f);
            go.GetComponent<Image>().color = CardBackColor;

            var view = go.AddComponent<CardView>();
            // M6-T4（B1）：费用独立底板占据左上角；名字右移避让，不再与费用重叠。
            view._costPanel = CreatePanel(go.transform, "CostPanel", new Vector2(-44f, 62f), new Vector2(30f, 28f));
            view._costText = CreateText(view._costPanel.transform, Vector2.zero, 16);
            view._nameText = CreateText(go.transform, "Name", new Vector2(13f, 62f), new Vector2(74f, 24f), 14);
            view._descriptionText = CreateText(go.transform, "Desc", new Vector2(0f, 12f), new Vector2(112f, 56f), 11);
            view._attackPanel = CreatePanel(go.transform, "AttackPanel", new Vector2(-38f, -62f), new Vector2(30f, 26f));
            view._healthPanel = CreatePanel(go.transform, "HealthPanel", new Vector2(38f, -62f), new Vector2(30f, 26f));
            view._attackText = CreateText(view._attackPanel.transform, Vector2.zero, 15);
            view._healthText = CreateText(view._healthPanel.transform, Vector2.zero, 15);
            view._playableHighlight = CreateHighlight(go.transform, "Playable", Color.green);
            view._attackableHighlight = CreateHighlight(go.transform, "Attackable", Color.red);
            view._tauntHighlight = CreateHighlight(go.transform, "Taunt", TauntColor);

            go.SetActive(false);
            return view;
        }

        /// <summary>构建未激活的浮动数字预制件。</summary>
        public static FloatingTextView BuildFloatingTextPrefab()
        {
            var go = new GameObject("FloatingTextPrefab", typeof(RectTransform), typeof(CanvasGroup));
            var root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(160f, 44f);

            TMP_Text text = CreateText(go.transform, "Text", Vector2.zero, new Vector2(160f, 44f), 22);
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;

            var view = go.AddComponent<FloatingTextView>();
            view._text = text;
            view._canvasGroup = go.GetComponent<CanvasGroup>();
            view._risePerSecond = 70f;
            go.SetActive(false);
            return view;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Attach(go, parent, position, size);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            return go;
        }

        private static GameObject CreateHighlight(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            go.SetActive(false);
            return go;
        }

        private static TMP_Text CreateText(Transform parent, Vector2 position, int fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_Text CreateText(
            Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Attach(go, parent, position, size);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Attach(GameObject go, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
