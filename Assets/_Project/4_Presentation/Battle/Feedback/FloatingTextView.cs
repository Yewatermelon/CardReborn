using Card.Core;
using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 浮动文字视图（M5-T6）：伤害/治疗/疲劳数字，上浮 + 渐隐，到时自动隐藏。
    /// <c>Update</c> 仅透传 <see cref="Tick"/>（U-3，表现插值）；时长由调用方按设置缩放后传入。
    /// </summary>
    public sealed class FloatingTextView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text _text = null!;
        [SerializeField] internal CanvasGroup _canvasGroup = null!;
        [SerializeField] internal float _risePerSecond = 60f;

        private RectTransform _rect = null!;
        private Vector2 _origin;
        private float _elapsed;

        /// <summary>本次播放的总时长（已被设置加速缩放），供测试断言。</summary>
        internal float Duration { get; private set; }

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// 惰性缓存 RectTransform：EditMode 下 SetActive(true) 不触发 Awake
        /// （池克隆自禁用预制体，Awake 不保证已执行），不能依赖 Awake 初始化。
        /// </summary>
        private RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = (RectTransform)transform;
                }

                return _rect;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>以当前位置为起点播放；duration 必须为正。</summary>
        public void Show(string content, Color color, float duration)
        {
            Guard.NotNull(content, nameof(content));

            _text.text = content;
            _text.color = color;
            _canvasGroup.alpha = 1f;
            _origin = Rect.anchoredPosition;
            _elapsed = 0f;
            Duration = Mathf.Max(0.0001f, duration);
            IsPlaying = true;
            gameObject.SetActive(true);
        }

        internal void Tick(float dt)
        {
            if (!IsPlaying)
            {
                return;
            }

            _elapsed += dt;
            float t = _elapsed / Duration;
            if (t >= 1f)
            {
                _canvasGroup.alpha = 0f;
                IsPlaying = false;
                gameObject.SetActive(false);
                return;
            }

            _canvasGroup.alpha = 1f - t;
            Rect.anchoredPosition = _origin + Vector2.up * (_risePerSecond * _elapsed);
        }

        /// <summary>池归还时强制停止（不播完）。</summary>
        internal void Stop()
        {
            IsPlaying = false;
            gameObject.SetActive(false);
        }
    }
}
