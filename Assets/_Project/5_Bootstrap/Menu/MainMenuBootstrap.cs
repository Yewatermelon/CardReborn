using Card.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Card.Bootstrap.Menu
{
    /// <summary>
    /// 主菜单引导器（M6-T1）：代码生成标题与"开始对战"按钮，点击载入 Battle 场景。
    /// 场景内唯一脚本；UI 全部运行时构建（无手工 YAML 引用）。
    /// </summary>
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        public const string BattleSceneName = "Battle";

        private void Start()
        {
            Battle.SceneCamera.Ensure();
            Battle.UiEventSystem.Ensure();

            var canvasGo = new GameObject("MenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            CreateTitle(canvasGo.transform);
            CreateStartButton(canvasGo.transform);
        }

        private static void CreateTitle(Transform parent)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(900f, 160f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 64;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "CardReborn";
        }

        private static void CreateStartButton(Transform parent)
        {
            var go = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(360f, 110f);
            go.GetComponent<Image>().color = new Color(0.22f, 0.3f, 0.45f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 32;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "开始对战";

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                GameLog.Info(LogChannel.Boot, "主菜单：进入对战场景。");
                SceneManager.LoadScene(BattleSceneName);
            });
        }
    }
}
