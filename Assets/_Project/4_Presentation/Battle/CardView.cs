using Card.Core;
using Card.Domain.Config;
using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 卡牌只读视图（FR-8.1）：渲染名称/费用/攻/血/描述/美术键。
    /// 无任何规则判断；数据变化时由外部（HandView 等）调用 <see cref="SetData"/> 刷新。
    /// 高亮三态（M5-T6；FR-8.6）由 <see cref="CardHighlight"/> 旗标驱动，只切显隐。
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text _nameText = null!;
        [SerializeField] internal TMP_Text _descriptionText = null!;
        [SerializeField] internal TMP_Text _costText = null!;
        [SerializeField] internal TMP_Text _attackText = null!;
        [SerializeField] internal TMP_Text _healthText = null!;
        [SerializeField] internal GameObject _attackPanel = null!;
        [SerializeField] internal GameObject _healthPanel = null!;
        [SerializeField] internal GameObject _playableHighlight = null!;
        [SerializeField] internal GameObject _attackableHighlight = null!;
        [SerializeField] internal GameObject _tauntHighlight = null!;

        /// <summary>最近一次 <see cref="SetData"/> 数据的局内实例 Id；配置态为 null（M5-T6）。</summary>
        public int? InstanceId { get; private set; }

        /// <summary>用新数据刷新全部字段；随从显示攻/血面板，其他类型隐藏。</summary>
        public void SetData(ICardViewData data)
        {
            Guard.NotNull(data, nameof(data));

            _nameText.text = data.Name;
            _descriptionText.text = data.Description;
            _costText.text = data.Cost.ToString();
            InstanceId = data.InstanceId;

            bool isMinion = data.Type == CardType.Minion;
            _attackPanel.SetActive(isMinion);
            _healthPanel.SetActive(isMinion);
            if (isMinion)
            {
                _attackText.text = data.Attack.ToString();
                _healthText.text = data.Health.ToString();
            }
        }

        /// <summary>按旗标切三个高亮面板的显隐（FR-8.6）；不含任何规则判断。</summary>
        public void SetHighlight(CardHighlight highlight)
        {
            _playableHighlight.SetActive((highlight & CardHighlight.Playable) != 0);
            _attackableHighlight.SetActive((highlight & CardHighlight.Attackable) != 0);
            _tauntHighlight.SetActive((highlight & CardHighlight.Taunt) != 0);
        }
    }
}
