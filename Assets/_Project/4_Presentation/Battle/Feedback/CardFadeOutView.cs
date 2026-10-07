using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 死亡淡出视图（M5-T6；FR-8.2）：把所在 GameObject 的 <see cref="CanvasGroup"/> alpha
    /// 从 1 渐隐到 0。<see cref="CanvasGroup"/> 缺失时 <see cref="Play"/> 现加。
    /// 渐隐结束后保持 alpha=0，由池归还（<see cref="CardViewPool"/>）复位。
    /// </summary>
    public sealed class CardFadeOutView : MonoBehaviour
    {
        private CanvasGroup _canvasGroup = null!;
        private float _elapsed;

        /// <summary>本次播放的总时长（已被设置加速缩放），供测试断言。</summary>
        internal float Duration { get; private set; }

        public bool IsPlaying { get; private set; }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>从 alpha=1 开始渐隐；重复调用重新起播。</summary>
        public void Play(float duration)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 1f;
            _elapsed = 0f;
            Duration = Mathf.Max(0.0001f, duration);
            IsPlaying = true;
        }

        /// <summary>中止渐隐，保持当前 alpha（复位由池归还负责）。</summary>
        public void Stop()
        {
            IsPlaying = false;
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
                return;
            }

            _canvasGroup.alpha = 1f - t;
        }
    }
}
