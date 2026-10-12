using Card.Application.Launch;
using Card.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Card.Bootstrap.Menu
{
    /// <summary>
    /// 主菜单引导器（M6-T1）：代码生成标题与对战按钮，点击载入 Battle 场景。
    /// 场景内唯一脚本；UI 全部运行时构建（无手工 YAML 引用）。
    /// M7-OBS-1：新增"人机对战"按钮，设 PlayerPrefs("GameMode","PVE")；
    /// 原"开始对战"改名"双人对战"，显式设为 PVP 模式（覆盖默认）。
    /// </summary>
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        public const string BattleSceneName = "Battle";

        private void Start()
        {
            BootLogging.ConfigureOnce();
            // 联机双开验证必须：窗口不在前台时仍跑 Update（否则权威 tick 停止 + 心跳超时断线）
            UnityEngine.Application.runInBackground = true;
            Battle.SceneCamera.Ensure();
            Battle.UiEventSystem.Ensure();

            // M14-T4：启动参数指定联机角色（-host / -join）时直接进入联机大厅，免点击双开。
            NetLaunchAction launch = NetLaunch.Resolve(Battle.BattleSceneBootstrap.PvpNetDefaultPort);
            if (launch.Kind != NetLaunchKind.None)
            {
                PlayerPrefs.SetString(Battle.BattleSceneBootstrap.GameModePrefKey,
                    Battle.BattleSceneBootstrap.PvpNetMode);
                GameLog.Info(LogChannel.Boot, $"启动参数：进入联机大厅（{launch}）。");
                SceneManager.LoadScene(BattleSceneName);
                return;
            }

            var canvasGo = new GameObject("MenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            CreateTitle(canvasGo.transform);
            CreateStartButton(canvasGo.transform);
            CreatePveButton(canvasGo.transform);
            CreateNetButton(canvasGo.transform);
        }

        private static void CreateTitle(Transform parent)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(0f, 220f);
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
            rect.anchoredPosition = new Vector2(0f, 20f);
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
            label.text = "双人对战";

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                PlayerPrefs.SetString(Battle.BattleSceneBootstrap.GameModePrefKey, Battle.BattleSceneBootstrap.PvpMode);
                GameLog.Info(LogChannel.Boot, "主菜单：进入双人对战（热座）。");
                SceneManager.LoadScene(BattleSceneName);
            });
        }

        private static void CreatePveButton(Transform parent)
        {
            var go = new GameObject("PveButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(0f, -120f);
            rect.sizeDelta = new Vector2(360f, 110f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.4f, 0.25f, 1f);

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
            label.text = "人机对战";

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                PlayerPrefs.SetString(Battle.BattleSceneBootstrap.GameModePrefKey, Battle.BattleSceneBootstrap.PveMode);
                GameLog.Info(LogChannel.Boot, "主菜单：进入人机对战（Greedy AI）。");
                SceneManager.LoadScene(BattleSceneName);
            });
        }

        /// <summary>联机对战入口（M14-T3）：设 GameMode=PVPNet 载入 Battle（内置联机大厅）。</summary>
        private static void CreateNetButton(Transform parent)
        {
            var go = new GameObject("NetButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(0f, -260f);
            rect.sizeDelta = new Vector2(360f, 110f);
            go.GetComponent<Image>().color = new Color(0.35f, 0.28f, 0.45f, 1f);

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
            label.text = "联机对战（本机验证）";

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                PlayerPrefs.SetString(Battle.BattleSceneBootstrap.GameModePrefKey, Battle.BattleSceneBootstrap.PvpNetMode);
                GameLog.Info(LogChannel.Boot, "主菜单：进入联机对战（房主托管 · 仅用于本机验证）。");
                SceneManager.LoadScene(BattleSceneName);
            });
        }
    }
}
