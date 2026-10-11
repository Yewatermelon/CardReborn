using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 卡牌运动组件（M14-T2，Docs/05 §5.5 位置插值 + 进场动画）：
    /// <see cref="MoveTo"/> 平滑滑向布局槽位（重复调用从当前位置续播）；
    /// <see cref="BeginEntry"/> 新卡入场——位置立即落槽，淡入 + 缩放；
    /// <see cref="Stop"/> 无条件复位（<c>CardView.SetData</c> / 池归还复位契约）。
    /// 惰性取组件：EditMode 下 SetActive(true) 不触发 Awake，不依赖 Awake 缓存。
    /// </summary>
    public sealed class CardMotionView : MonoBehaviour
    {
        /// <summary>滑位补间时长（秒）。</summary>
        internal const float MoveDuration = 0.2f;

        /// <summary>进场动画时长（秒）。</summary>
        internal const float EntryDuration = 0.25f;

        /// <summary>进场起始缩放。</summary>
        internal const float EntryScaleFrom = 0.8f;

        private RectTransform? _rect;
        private CanvasGroup? _group;
        private Vector2 _from;
        private Vector2 _target;
        private float _elapsed;
        private bool _entry;

        public bool IsPlaying { get; private set; }

        /// <summary>平滑滑向目标槽位；已在目标位则立即完成；入场中途调用会先复位淡入/缩放。</summary>
        public void MoveTo(Vector2 target)
        {
            _rect ??= transform as RectTransform;
            if (_rect == null)
            {
                return;
            }

            _target = target;
            _from = _rect.anchoredPosition;
            transform.localScale = Vector3.one;
            if (_from == target)
            {
                Finish();
                return;
            }

            _entry = false;
            _elapsed = 0f;
            IsPlaying = true;
            EnsureGroup().alpha = 1f;
        }

        /// <summary>进场：位置立即落槽，从 80% 缩放 + 全透明淡入到常态。</summary>
        public void BeginEntry(Vector2 target)
        {
            _rect ??= transform as RectTransform;
            if (_rect == null)
            {
                return;
            }

            _target = target;
            _rect.anchoredPosition = target;
            _entry = true;
            _elapsed = 0f;
            IsPlaying = true;
            transform.localScale = Vector3.one * EntryScaleFrom;
            EnsureGroup().alpha = 0f;
        }

        /// <summary>无条件复位：停止补间、缩放/透明度归位（位置由随后的布局接管）。</summary>
        public void Stop()
        {
            IsPlaying = false;
            transform.localScale = Vector3.one;
            if (_group != null)
            {
                _group.alpha = 1f;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float dt)
        {
            if (!IsPlaying)
            {
                return;
            }

            _elapsed += dt;
            float t = _elapsed / (_entry ? EntryDuration : MoveDuration);
            if (t >= 1f)
            {
                Finish();
                return;
            }

            if (_entry)
            {
                transform.localScale = Vector3.one * (EntryScaleFrom + (1f - EntryScaleFrom) * t);
                _group!.alpha = t;
            }
            else
            {
                _rect!.anchoredPosition = Vector2.LerpUnclamped(_from, _target, t);
            }
        }

        private void Finish()
        {
            if (_entry)
            {
                transform.localScale = Vector3.one;
                if (_group != null)
                {
                    _group.alpha = 1f;
                }
            }
            else if (_rect != null)
            {
                _rect.anchoredPosition = _target;
            }

            IsPlaying = false;
        }

        private CanvasGroup EnsureGroup()
        {
            if (_group == null && !TryGetComponent(out _group))
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }

            return _group!;
        }
    }
}
