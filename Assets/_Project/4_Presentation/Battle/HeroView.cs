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
    }
}
