using Card.Presentation.Battle.Feedback;
using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 英雄视图（M5-T2）：渲染生命；护甲 &gt; 0 时显示护甲面板，为 0 隐藏。
    /// M14-T2（Docs/05 §5.5）：生命走 <see cref="NumberTween"/> 数值补间，不瞬时跳变；
    /// 护甲与名称即时刷新。
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        private readonly NumberTween _healthTween = new(NumberTween.DefaultDuration);

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
            _healthTween.To(health);
            RefreshHealth();

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

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>推进生命补间并刷新文本（EditMode 测试直接驱动）。</summary>
        internal void Tick(float dt)
        {
            if (!_healthTween.IsPlaying)
            {
                return;
            }

            _healthTween.Tick(dt);
            RefreshHealth();
        }

        private void RefreshHealth()
        {
            _healthText.text = _healthTween.Current.ToString();
        }
    }
}
