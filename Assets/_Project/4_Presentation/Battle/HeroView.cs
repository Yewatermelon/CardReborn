using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>英雄视图（M5-T2）：渲染生命；护甲 &gt; 0 时显示护甲面板，为 0 隐藏。</summary>
    public sealed class HeroView : MonoBehaviour
    {
        // 生命文本
        [SerializeField] internal TMP_Text _healthText = null!;
        // 护甲文本
        [SerializeField] internal TMP_Text _armorText = null!;
        // 护甲面板
        [SerializeField] internal GameObject _armorPanel = null!;
        // 英雄名文本（M6-T1：灰板占位，渲染配置 NameKey）
        [SerializeField] internal TMP_Text _nameText = null!;

        public void SetData(int health, int maxHealth, int armor)
        {
            _healthText.text = health.ToString();

            bool hasArmor = armor > 0;
            _armorPanel.SetActive(hasArmor);
            if (hasArmor)
            {
                _armorText.text = armor.ToString();
            }
        }

        /// <summary>渲染英雄名（NameKey，M9 接本地化文案）。</summary>
        public void SetName(string nameKey)
        {
            _nameText.text = nameKey;
        }
    }
}
