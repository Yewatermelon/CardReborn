using System;
using Card.Bootstrap.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Replay
{
    /// <summary>
    /// 回放控制条（M15-T1）：底部两行面板——上行回放文件切换，下行播放控制
    /// （上一步 / 播放暂停 / 下一步 / 倍速循环 / 进度 Slider / 帧号）。
    /// 只读回放不产生命令上行；控件语义由 <see cref="ReplaySceneBootstrap"/> 绑定。
    /// </summary>
    internal sealed class ReplayControlBar
    {
        public event Action? PlayPauseClicked;
        public event Action? StepBackClicked;
        public event Action? StepForwardClicked;
        public event Action? SpeedClicked;
        public event Action<int>? ReplaySwitched;   // -1 上一个文件 / +1 下一个
        public event Action<int>? SliderSeekRequested;

        private readonly TMP_Text _fileLabel;
        private readonly TMP_Text _frameLabel;
        private readonly TMP_Text _speedLabel;
        private readonly TMP_Text _playLabel;
        private readonly Slider _slider;
        private bool _suppressSlider; // 程序驱动 SetValue 不回环触发 Seek

        public ReplayControlBar(RectTransform canvasRoot)
        {
            RectTransform bar = BattleUiPrimitive.CreateBox(
                "ReplayControlBar", canvasRoot, new Vector2(0f, 12f), new Vector2(1720f, 112f));
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);

            BuildFileRow(bar, out _fileLabel);
            BuildControlRow(bar, out _playLabel, out _speedLabel, out _frameLabel, out _slider);
        }

        /// <summary>显示当前回放文件名。</summary>
        public void SetFile(string name)
        {
            _fileLabel.text = name;
        }

        /// <summary>更新帧号文本与进度条（程序驱动不触发 Seek 回调）。</summary>
        public void SetFrames(int current, int total)
        {
            _frameLabel.text = total > 0
                ? current + " / " + total + " 步"
                : "-- / --";

            _suppressSlider = true;
            _slider.maxValue = total > 0 ? total : 1;
            _slider.interactable = total > 0;
            _slider.SetValueWithoutNotify(current);
            _suppressSlider = false;
        }

        /// <summary>更新倍速标签与播放/暂停按钮文本。</summary>
        public void SetSpeed(float speed)
        {
            _speedLabel.text = ((int)speed).ToString() + "x";
        }

        public void SetPlaying(bool playing)
        {
            _playLabel.text = playing ? "暂停" : "播放";
        }

        private void BuildFileRow(RectTransform bar, out TMP_Text fileLabel)
        {
            (Button prev, _) = MakeButton(bar, "PrevReplayBtn", new Vector2(-800f, 27f), new Vector2(64f, 40f), "◀");
            (Button next, _) = MakeButton(bar, "NextReplayBtn", new Vector2(800f, 27f), new Vector2(64f, 40f), "▶");
            var fileHost = BattleUiPrimitive.CreateBox("FileLabel", bar, new Vector2(0f, 27f), new Vector2(1500f, 40f));
            fileLabel = BattleUiPrimitive.Text(fileHost, "（无回放）", 26);

            prev.onClick.AddListener(() => ReplaySwitched?.Invoke(-1));
            next.onClick.AddListener(() => ReplaySwitched?.Invoke(+1));
        }

        private void BuildControlRow(
            RectTransform bar,
            out TMP_Text playLabel,
            out TMP_Text speedLabel,
            out TMP_Text frameLabel,
            out Slider slider)
        {
            (Button stepBack, _) = MakeButton(bar, "StepBackBtn", new Vector2(-810f, -26f), new Vector2(72f, 40f), "后退");
            (Button playPause, TMP_Text playText) = MakeButton(bar, "PlayPauseBtn", new Vector2(-700f, -26f), new Vector2(120f, 40f), "播放");
            playLabel = playText;
            (Button stepForward, _) = MakeButton(bar, "StepForwardBtn", new Vector2(-590f, -26f), new Vector2(72f, 40f), "前进");
            (Button speed, TMP_Text speedText) = MakeButton(bar, "SpeedBtn", new Vector2(-470f, -26f), new Vector2(96f, 40f), "1x");
            speedLabel = speedText;
            (Slider sliderControl, _) = MakeSlider(bar, "FrameSlider", new Vector2(155f, -26f), new Vector2(1170f, 22f));
            slider = sliderControl;
            var frameHost = BattleUiPrimitive.CreateBox("FrameLabel", bar, new Vector2(810f, -26f), new Vector2(160f, 40f));
            frameLabel = BattleUiPrimitive.Text(frameHost, "-- / --", 26);

            playPause.onClick.AddListener(() => PlayPauseClicked?.Invoke());
            stepBack.onClick.AddListener(() => StepBackClicked?.Invoke());
            stepForward.onClick.AddListener(() => StepForwardClicked?.Invoke());
            speed.onClick.AddListener(() => SpeedClicked?.Invoke());
            slider.onValueChanged.AddListener(value =>
            {
                if (!_suppressSlider)
                {
                    SliderSeekRequested?.Invoke(Mathf.RoundToInt(value));
                }
            });
        }

        private static (Button button, TMP_Text label) MakeButton(
            Transform parent, string name, Vector2 position, Vector2 size, string label)
        {
            RectTransform host = BattleUiPrimitive.CreateBox(name, parent, position, size);
            Button button = BattleUiPrimitive.Button(host, out _);
            TMP_Text text = BattleUiPrimitive.Text(host, label, 28);
            return (button, text);
        }

        private static (Slider slider, Image image) MakeSlider(
            Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rootGo = new GameObject(name, typeof(RectTransform));
            BattleUiPrimitive.Place(rootGo, parent, position, size);
            var root = (RectTransform)rootGo.transform;

            RectTransform background = BuildSliderPart(root, "Background", 14f, new Color(1f, 1f, 1f, 0.15f), driven: false);
            background.offsetMin = new Vector2(-10f, -7f);
            background.offsetMax = new Vector2(10f, 7f);
            RectTransform fillArea = BuildSliderPart(root, "Fill Area", 10f, Color.clear, driven: false);
            fillArea.offsetMin = new Vector2(-10f, -5f);
            fillArea.offsetMax = new Vector2(10f, 5f);
            RectTransform fill = BuildSliderPart(fillArea, "Fill", 0f, new Color(0.35f, 0.65f, 0.95f, 1f), driven: true);
            RectTransform handleArea = BuildSliderPart(root, "Handle Area", 0f, Color.clear, driven: false);
            handleArea.offsetMin = new Vector2(-10f, 0f);
            handleArea.offsetMax = new Vector2(10f, 0f);
            RectTransform handle = BuildSliderPart(handleArea, "Handle", 0f, new Color(0.9f, 0.95f, 1f, 1f), driven: true);
            handle.sizeDelta = new Vector2(18f, 0f);

            var slider = rootGo.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            return (slider, handle.GetComponent<Image>());
        }

        /// <summary>
        /// 构建 Slider 子部件：driven=false 为水平拉伸静态区（anchor 铺满）；
        /// driven=true 为被 Slider 驱动的填充/手柄（左边缘随值移动，右/上下铺满）。
        /// </summary>
        private static RectTransform BuildSliderPart(
            Transform parent, string name, float height, Color color, bool driven)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            BattleUiPrimitive.Place(go, parent, Vector2.zero, Vector2.zero);
            go.GetComponent<Image>().color = color;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = driven ? new Vector2(0f, 0f) : new Vector2(0f, 0.5f);
            rect.anchorMax = driven ? new Vector2(0f, 1f) : new Vector2(1f, 0.5f);
            rect.sizeDelta = driven ? Vector2.zero : new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }
    }
}
