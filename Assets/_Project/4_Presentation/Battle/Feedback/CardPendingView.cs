using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 待确认可视组件（M14-T1，Docs/05 §6.1 表现先行）：出牌命令上行后立即把卡牌
    /// 置为"半透明 + 上移"的待确认态，<strong>只改视觉，不改数据</strong>——
    /// 权威接受后 <see cref="Confirm"/> 复位（落位由状态同步重渲染驱动），
    /// 拒绝后 <see cref="Rollback"/> 复位（"卡牌飞回手牌"），
    /// 池复用前由 <c>CardView.SetData</c> 调 <see cref="Stop"/> 无条件复位（复用即全量复位契约）。
    /// 惰性取组件：EditMode 下 SetActive(true) 不触发 Awake，不依赖 Awake 缓存。
    /// </summary>
    public sealed class CardPendingView : MonoBehaviour
    {
        /// <summary>待确认态透明度（保持可见，提示"等待权威确认"）。</summary>
        internal const float PendingAlpha = 0.5f;

        /// <summary>待确认态上移偏移（"卡牌飞向战场"的占位反馈，像素）。</summary>
        internal const float PendingOffsetY = 40f;

        private CanvasGroup? _group;
        private RectTransform? _rect;
        private Vector2 _origin;
        private bool _originCaptured;

        /// <summary>当前是否处于待确认态。</summary>
        public bool IsPending { get; private set; }

        /// <summary>进入待确认态；已处于待确认态时重复调用无副作用。</summary>
        public void ShowPending()
        {
            if (IsPending)
            {
                return;
            }

            _group = GetOrAddGroup();
            _rect = transform as RectTransform;
            if (_rect != null)
            {
                _origin = _rect.anchoredPosition;
                _originCaptured = true;
                _rect.anchoredPosition = _origin + new Vector2(0f, PendingOffsetY);
            }

            _group.alpha = PendingAlpha;
            IsPending = true;
        }

        /// <summary>权威拒绝后的回退复位（"卡牌飞回手牌"）。</summary>
        public void Rollback()
        {
            Stop();
        }

        /// <summary>权威接受后的确认复位（落位交由状态同步重渲染）。</summary>
        public void Confirm()
        {
            Stop();
        }

        /// <summary>无条件复位（SetData 复位路径 / 池归还）；未进入过待确认态时安全。</summary>
        public void Stop()
        {
            if (_originCaptured && _rect != null)
            {
                _rect.anchoredPosition = _origin;
            }

            if (_group != null)
            {
                _group.alpha = 1f;
            }

            IsPending = false;
            _originCaptured = false;
        }

        private CanvasGroup GetOrAddGroup()
        {
            if (_group == null && !TryGetComponent(out _group))
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }

            return _group!;
        }
    }
}
