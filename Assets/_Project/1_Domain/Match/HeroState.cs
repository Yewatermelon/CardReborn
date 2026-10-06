using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// 英雄运行时状态：以 Key 引用 <c>HeroDefinition</c>（配置与状态分离，Docs/03 §5.9）。
    /// 伤害/治疗/护甲的结算行为在 M4/T7 实现，本类型此刻只承载数据。
    /// </summary>
    public sealed class HeroState
    {
        public HeroState(string heroKey, string heroPowerKey, int maxHealth)
            : this(heroKey, heroPowerKey, maxHealth, maxHealth)
        {
        }

        public HeroState(string heroKey, string heroPowerKey, int maxHealth, int health)
        {
            HeroKey = Guard.NotNullOrWhiteSpace(heroKey, nameof(heroKey));
            HeroPowerKey = Guard.NotNullOrWhiteSpace(heroPowerKey, nameof(heroPowerKey));
            MaxHealth = Guard.Positive(maxHealth, nameof(maxHealth));
            Health = Guard.InRange(health, 0, maxHealth + 1, nameof(health));
        }

        /// <summary>英雄配置 Key（指向 Heroes 表）。</summary>
        public string HeroKey { get; }

        /// <summary>英雄技能配置 Key（指向 HeroPowers 表）。</summary>
        public string HeroPowerKey { get; }

        public int MaxHealth { get; }

        /// <summary>当前生命；运行期允许经结算降至 ≤ 0（T7 判负），构造时必须为正值。</summary>
        public int Health { get; set; }

        /// <summary>护甲（先抵伤害，GainArmorEffect 写入）。</summary>
        public int Armor { get; set; }

        /// <summary>英雄技能本回合是否已使用（FR-5.10：每回合 1 次，回合切换时重置）。</summary>
        public bool PowerUsedThisTurn { get; set; }
    }
}
