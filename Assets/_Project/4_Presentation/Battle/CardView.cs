using System;
using Card.Core;
using Card.Domain.Config;
using Card.Presentation.Battle.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 卡牌只读视图（FR-8.1）：渲染名称/费用/攻/血/描述/美术键。
    /// 无任何规则判断；数据变化时由外部（HandView 等）调用 <see cref="SetData"/> 刷新。
    /// 高亮三态（M5-T6；FR-8.6）由 <see cref="CardHighlight"/> 旗标驱动，只切显隐。
    /// M14-T2（Docs/05 §5.5）：同实例攻/血更新走 <see cref="NumberTween"/> 数值补间；
    /// 复用换实例或首次赋值立即 Snap（复用即全量复位契约）。
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        private readonly NumberTween _attackTween = new(NumberTween.DefaultDuration);
        private readonly NumberTween _healthTween = new(NumberTween.DefaultDuration);

        [SerializeField] internal TMP_Text _nameText = null!;
        [SerializeField] internal TMP_Text _descriptionText = null!;
        [SerializeField] internal TMP_Text _costText = null!;
        [SerializeField] internal GameObject _costPanel = null!;
        [SerializeField] internal TMP_Text _attackText = null!;
        [SerializeField] internal TMP_Text _healthText = null!;
        [SerializeField] internal GameObject _attackPanel = null!;
        [SerializeField] internal GameObject _healthPanel = null!;
        [SerializeField] internal GameObject _playableHighlight = null!;
        [SerializeField] internal GameObject _attackableHighlight = null!;
        [SerializeField] internal GameObject _tauntHighlight = null!;

        /// <summary>最近一次 <see cref="SetData"/> 数据的局内实例 Id；配置态为 null（M5-T6）。</summary>
        public int? InstanceId { get; private set; }

        private Action? _clickHandler;

        /// <summary>
        /// 绑定点击回调（M6-T1）：按需挂 <see cref="Button"/>；重复绑定先清旧回调。
        /// 视图只读语义不变——点击意图由上层（HandView/装配器）翻译为命令。
        /// </summary>
        public void BindClick(Action onClick)
        {
            Guard.NotNull(onClick, nameof(onClick));
            UnbindClick();
            _clickHandler = onClick;
            GetOrAddButton().onClick.AddListener(RaiseClick);
        }

        /// <summary>解除点击回调（池归还时必须调用，防止旧卡串台）。</summary>
        public void UnbindClick()
        {
            if (_clickHandler == null)
            {
                return;
            }

            if (TryGetComponent(out Button button))
            {
                button.onClick.RemoveListener(RaiseClick);
            }

            _clickHandler = null;
        }

        private Button GetOrAddButton()
        {
            if (!TryGetComponent(out Button button))
            {
                button = gameObject.AddComponent<Button>();
            }

            return button;
        }

        private void RaiseClick()
        {
            _clickHandler?.Invoke();
        }

        /// <summary>用新数据刷新全部字段；随从显示攻/血面板，其他类型隐藏。</summary>
        public void SetData(ICardViewData data)
        {
            Guard.NotNull(data, nameof(data));

            int? previousId = InstanceId;
            bool sameInstance = previousId.HasValue && Nullable.Equals(previousId, data.InstanceId);

            _nameText.text = data.Name;
            _descriptionText.text = data.Description;
            _costText.text = data.Cost.ToString();
            InstanceId = data.InstanceId;

            bool isMinion = data.Type == CardType.Minion;
            _attackPanel.SetActive(isMinion);
            _healthPanel.SetActive(isMinion);
            if (isMinion)
            {
                if (sameInstance)
                {
                    _attackTween.To(data.Attack);
                    _healthTween.To(data.Health);
                }
                else
                {
                    _attackTween.Snap(data.Attack);
                    _healthTween.Snap(data.Health);
                }

                RefreshCombatText();
            }

            // 复用场景（池化 + 卡数不变时 SetCards 直接覆盖 SetData）：
            // 之前可能正在播死亡淡出（alpha 渐减 + IsPlaying=true），
            // 若不中止会把新卡也拖到不可见——表现层"隐形卡"bug（M6-B7）。
            if (TryGetComponent(out CardFadeOutView fade))
            {
                fade.Stop();
            }

            // 待确认视觉（M14-T1）与入场/滑位（M14-T2）同属可视副作用：
            // 复用前必须复位 alpha/位置/缩放（复用即全量复位契约）。
            if (TryGetComponent(out CardPendingView pending))
            {
                pending.Stop();
            }

            if (TryGetComponent(out CardMotionView motion))
            {
                motion.Stop();
            }

            if (TryGetComponent(out CanvasGroup group))
            {
                group.alpha = 1f;
            }
        }

        /// <summary>按旗标切三个高亮面板的显隐（FR-8.6）；不含任何规则判断。</summary>
        public void SetHighlight(CardHighlight highlight)
        {
            _playableHighlight.SetActive((highlight & CardHighlight.Playable) != 0);
            _attackableHighlight.SetActive((highlight & CardHighlight.Attackable) != 0);
            _tauntHighlight.SetActive((highlight & CardHighlight.Taunt) != 0);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>推进攻/血补间并刷新文本（EditMode 测试直接驱动）。</summary>
        internal void Tick(float dt)
        {
            if (!_attackTween.IsPlaying && !_healthTween.IsPlaying)
            {
                return;
            }

            _attackTween.Tick(dt);
            _healthTween.Tick(dt);
            RefreshCombatText();
        }

        private void RefreshCombatText()
        {
            _attackText.text = _attackTween.Current.ToString();
            _healthText.text = _healthTween.Current.ToString();
        }
    }
}
