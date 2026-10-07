using Card.Core;
using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 回合/终局横幅视图（M5-T6）：居中提示文本，到时自动隐藏；<c>Update</c> 仅透传 <see cref="Tick"/>（U-3）。
    /// 渐显渐隐等美术打磨属 M9。
    /// </summary>
    public sealed class TurnBannerView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text _text = null!;

        private float _elapsed;

        /// <summary>本次播放的总时长（已被设置加速缩放），供测试断言。</summary>
        internal float Duration { get; private set; }

        public bool IsPlaying { get; private set; }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>显示横幅并开始计时；播中再调则重置计时与文本。duration 必须为正。</summary>
        public void Show(string content, float duration)
        {
            Guard.NotNull(content, nameof(content));

            _text.text = content;
            _elapsed = 0f;
            Duration = Mathf.Max(0.0001f, duration);
            IsPlaying = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            IsPlaying = false;
            gameObject.SetActive(false);
        }

        internal void Tick(float dt)
        {
            if (!IsPlaying)
            {
                return;
            }

            _elapsed += dt;
            if (_elapsed >= Duration)
            {
                Hide();
            }
        }
    }
}
