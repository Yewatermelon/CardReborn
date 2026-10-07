using Card.Core;
using Card.Domain.Config;
using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 卡牌只读视图（FR-8.1）：渲染名称/费用/攻/血/描述/美术键。
    /// 无任何规则判断；数据变化时由外部（HandView 等）调用 <see cref="SetData"/> 刷新。
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

        /// <summary>用新数据刷新全部字段；随从显示攻/血面板，其他类型隐藏。</summary>
        public void SetData(ICardViewData data)
        {
            Guard.NotNull(data, nameof(data));

            _nameText.text = data.Name;
            _descriptionText.text = data.Description;
            _costText.text = data.Cost.ToString();

            bool isMinion = data.Type == CardType.Minion;
            _attackPanel.SetActive(isMinion);
            _healthPanel.SetActive(isMinion);
            if (isMinion)
            {
                _attackText.text = data.Attack.ToString();
                _healthText.text = data.Health.ToString();
            }
        }
    }
}
