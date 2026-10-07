using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 战斗 UI 图元构建（M6-T1）：从 <see cref="BattleUiFactory"/> 拆出的底层矩形/
    /// 面板/文本/按钮原语，只负责摆放与外观，不含布局语义。供工厂内部静态导入使用。
    /// </summary>
    internal static class BattleUiPrimitive
    {
        internal static RectTransform CreateBox(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place(go, parent, position, size);
            return (RectTransform)go.transform;
        }

        internal static void Place(
            GameObject go, Transform parent, Vector2 position, Vector2 size, bool stretch = false)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            if (stretch)
            {
                Stretch(rect);
            }
            else
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static GameObject Panel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Place(go, parent, position, size);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            return go;
        }

        internal static TMP_Text Text(RectTransform parent, string value, int fontSize)
        {
            TMP_Text text = Text(parent, value, Vector2.zero, Vector2.zero, fontSize);
            Stretch((RectTransform)text.transform);
            return text;
        }

        internal static TMP_Text Text(
            Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place(go, parent, position, size);
            var text = go.AddComponent<TextMeshProUGUI>();
            // 字体不显式指定：跟随 TMP 全局默认（中文资产由 Card/M6/2 生成并设默认）。
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = name;
            return text;
        }

        internal static Button Button(RectTransform host, out ColorBlock colors)
        {
            var image = host.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.3f, 0.45f, 1f);
            var button = host.gameObject.AddComponent<Button>();
            colors = button.colors;
            return button;
        }
    }
}
