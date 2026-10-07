using TMPro;
using UnityEngine;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>表现层测试共享夹具：以代码组装全字段连线的 CardView 预制体（M5-T2）。</summary>
    internal static class PresentationTestPrefabs
    {
        public static CardView CreateCardViewPrefab()
        {
            var go = new GameObject("CardViewPrefab");
            CardView view = go.AddComponent<CardView>();
            view._nameText = AddText(go.transform, "Name");
            view._descriptionText = AddText(go.transform, "Desc");
            view._costText = AddText(go.transform, "Cost");
            view._attackText = AddText(go.transform, "Attack");
            view._healthText = AddText(go.transform, "Health");
            view._attackPanel = AddPanel(go.transform, "AttackPanel");
            view._healthPanel = AddPanel(go.transform, "HealthPanel");
            // M5-T6：高亮三面板 + 死亡淡出所需的 CanvasGroup
            view._playableHighlight = AddPanel(go.transform, "PlayableHighlight");
            view._attackableHighlight = AddPanel(go.transform, "AttackableHighlight");
            view._tauntHighlight = AddPanel(go.transform, "TauntHighlight");
            go.AddComponent<CanvasGroup>();
            return view;
        }

        private static TMP_Text AddText(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static GameObject AddPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go;
        }
    }
}
