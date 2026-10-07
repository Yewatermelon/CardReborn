using Card.Presentation.Battle.Feedback;
using TMPro;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>反馈子系统测试夹具（M5-T6）：以代码组装全字段连线的浮动文字/回合横幅预制。</summary>
    internal static class FeedbackTestPrefabs
    {
        public static FloatingTextView CreateFloatingTextView()
        {
            var go = new GameObject("FloatingTextPrefab", typeof(RectTransform));
            var view = go.AddComponent<FloatingTextView>();
            view._text = AddText(go.transform, "Text");
            view._canvasGroup = go.AddComponent<CanvasGroup>();
            view._risePerSecond = 60f;
            go.SetActive(false);
            return view;
        }

        public static TurnBannerView CreateTurnBannerView()
        {
            var go = new GameObject("TurnBannerPrefab", typeof(RectTransform));
            var view = go.AddComponent<TurnBannerView>();
            view._text = AddText(go.transform, "Text");
            go.SetActive(false);
            return view;
        }

        private static TMP_Text AddText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<TextMeshProUGUI>();
        }
    }
}
