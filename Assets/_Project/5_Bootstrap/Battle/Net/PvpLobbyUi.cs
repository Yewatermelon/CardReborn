using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>大厅交互阶段（M14-T3）：角色选择 → 准备等待 → 已开局隐藏。</summary>
    internal enum PvpLobbyPhase
    {
        Choose = 0,
        WaitingReady = 1,
        Hidden = 2,
    }

    /// <summary>
    /// 联机大厅 overlay（M14-T3）：标题、副标签"房主托管 · 仅用于本机验证"、
    /// 房主/加入按钮、地址输入框（记忆上次地址）、准备按钮、状态文本、返回按钮。
    /// 只负责呈现与点击事件，不判断流程（状态机归 <see cref="PvpLobbyFlow"/>）。
    /// </summary>
    internal sealed class PvpLobbyUi
    {
        /// <summary>地址记忆键（PlayerPrefs）。</summary>
        public const string AddressPrefKey = "PvpJoinAddress";

        /// <summary>副标签文案（AC-14：界面标注）。</summary>
        public const string SubtitleText = "房主托管 · 仅用于本机验证";

        /// <summary>地址输入默认值（空输入时回落）。</summary>
        public const string DefaultAddress = "127.0.0.1:7777";

        private readonly GameObject _root;
        private readonly GameObject _chooseGroup;
        private readonly GameObject _readyGroup;

        internal PvpLobbyUi(GameObject root, GameObject chooseGroup, GameObject readyGroup,
            TMP_InputField addressInput, TMP_Text statusText)
        {
            _root = root;
            _chooseGroup = chooseGroup;
            _readyGroup = readyGroup;
            AddressInput = addressInput;
            StatusText = statusText;
        }

        public GameObject Root => _root;

        public TMP_InputField AddressInput { get; }

        public TMP_Text StatusText { get; }

        /// <summary>点击回调（属性而非 event：工厂需在外部触发 Invoke）。</summary>
        public Action? HostClicked { get; set; }

        public Action? JoinClicked { get; set; }

        public Action? ReadyClicked { get; set; }

        public Action? BackClicked { get; set; }

        /// <summary>当前地址输入（空回落默认）。</summary>
        public string Address => string.IsNullOrWhiteSpace(AddressInput.text)
            ? DefaultAddress
            : AddressInput.text.Trim();

        public void SetStatus(string text)
        {
            StatusText.text = text;
            StatusText.color = text.StartsWith("!") ? new Color(1f, 0.4f, 0.35f) : Color.white;
        }

        public void SetPhase(PvpLobbyPhase phase)
        {
            _root.SetActive(phase != PvpLobbyPhase.Hidden);
            _chooseGroup.SetActive(phase == PvpLobbyPhase.Choose);
            _readyGroup.SetActive(phase == PvpLobbyPhase.WaitingReady);
        }

        /// <summary>记忆地址（进入准备阶段时保存）。</summary>
        public void RememberAddress()
        {
            PlayerPrefs.SetString(AddressPrefKey, AddressInput.text);
        }
    }

    /// <summary>大厅 overlay 运行时工厂（M6-T1 模式：代码建 UI，无手工 YAML 引用）。</summary>
    internal static class PvpLobbyUiFactory
    {
        public static PvpLobbyUi Create(Transform parent)
        {
            GameObject root = BattleUiPrimitive.Panel(parent, "PvpLobbyRoot", Vector2.zero, Vector2.zero);
            BattleUiPrimitive.Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            CreateLabel(root.transform, "Title", "联机对战", 48, 240f);
            CreateLabel(root.transform, "Subtitle", PvpLobbyUi.SubtitleText, 22, 180f, Color.gray);
            CreateLabel(root.transform, "AddressHint", "房主地址（host:port）", 18, -190f, Color.gray);

            GameObject chooseGroup = new GameObject("ChooseGroup", typeof(RectTransform));
            BattleUiPrimitive.Place(chooseGroup, root.transform, Vector2.zero, Vector2.zero, stretch: true);

            Button hostButton = CreateMenuButton(chooseGroup.transform, "HostButton", "创建对局（房主）", 80f);
            Button joinButton = CreateMenuButton(chooseGroup.transform, "JoinButton", "加入对局", -20f);
            TMP_InputField addressInput = CreateAddressInput(chooseGroup.transform, -130f);

            GameObject readyGroup = new GameObject("ReadyGroup", typeof(RectTransform));
            BattleUiPrimitive.Place(readyGroup, root.transform, Vector2.zero, Vector2.zero, stretch: true);
            readyGroup.SetActive(false);
            Button readyButton = CreateMenuButton(readyGroup.transform, "ReadyButton", "准备", 40f);

            TMP_Text statusText = BattleUiPrimitive.Text(root.transform, "StatusText",
                new Vector2(0f, -300f), new Vector2(860f, 80f), 24);
            statusText.text = string.Empty;
            statusText.raycastTarget = false;

            Button backButton = CreateMenuButton(root.transform, "BackButton", "返回", -400f, width: 200f);

            var ui = new PvpLobbyUi(root, chooseGroup, readyGroup, addressInput, statusText);
            addressInput.text = PlayerPrefs.GetString(PvpLobbyUi.AddressPrefKey, PvpLobbyUi.DefaultAddress);
            hostButton.onClick.AddListener(() => ui.HostClicked?.Invoke());
            joinButton.onClick.AddListener(() => ui.JoinClicked?.Invoke());
            readyButton.onClick.AddListener(() => ui.ReadyClicked?.Invoke());
            backButton.onClick.AddListener(() => ui.BackClicked?.Invoke());
            return ui;
        }

        private static void CreateLabel(Transform parent, string name, string value, int size, float y,
            Color? color = null)
        {
            TMP_Text text = BattleUiPrimitive.Text(parent, name,
                new Vector2(0f, y), new Vector2(860f, 70f), size);
            text.text = value;
            text.raycastTarget = false;
            if (color.HasValue)
            {
                text.color = color.Value;
            }
        }

        private static Button CreateMenuButton(Transform parent, string name, string label,
            float y, float width = 360f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            BattleUiPrimitive.Place(go, parent, new Vector2(0f, y), new Vector2(width, 76f));
            go.GetComponent<Image>().color = new Color(0.22f, 0.3f, 0.45f, 1f);
            Button button = go.AddComponent<Button>();
            TMP_Text text = BattleUiPrimitive.Text((RectTransform)go.transform, label, 30);
            text.raycastTarget = false;
            return button;
        }

        private static TMP_InputField CreateAddressInput(Transform parent, float y)
        {
            var go = new GameObject("AddressInput", typeof(RectTransform), typeof(Image));
            BattleUiPrimitive.Place(go, parent, new Vector2(0f, y), new Vector2(360f, 56f));
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 1f);

            TMP_Text text = BattleUiPrimitive.Text((RectTransform)go.transform, string.Empty, 24);
            text.alignment = TextAlignmentOptions.Left;
            text.margin = new Vector4(12f, 0f, 12f, 0f);

            var input = go.AddComponent<TMP_InputField>();
            input.textComponent = text;
            input.textViewport = (RectTransform)text.transform;
            return input;
        }
    }
}
