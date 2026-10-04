using System;
using System.Collections.Generic;

namespace Card.Domain.Config
{
    /// <summary>
    /// 卡牌配置（只读契约，Docs/01 第 4.1 节）。运行时状态用 CardInstance，两者分离。
    /// 字段校验（范围、引用、唯一性）由导入器的校验器负责（M2-T4）。
    /// </summary>
    public sealed class CardDefinition
    {
        public int Id { get; init; }

        /// <summary>稳定英文键（代码与资源引用它）；一经发布不得修改。</summary>
        public string Key { get; init; } = string.Empty;

        public string NameKey { get; init; } = string.Empty;

        public string DescKey { get; init; } = string.Empty;

        public int Cost { get; init; }

        public CardType Type { get; init; } = CardType.Minion;

        public CardRarity Rarity { get; init; } = CardRarity.Common;

        public CardClass Class { get; init; } = CardClass.Neutral;

        /// <summary>攻击力（随从有效；法术为 0）。</summary>
        public int Attack { get; init; }

        /// <summary>生命值（随从有效；法术为 0）。</summary>
        public int Health { get; init; }

        public Keyword Keywords { get; init; } = Keyword.None;

        public TargetRule TargetRule { get; init; } = TargetRule.None;

        /// <summary>效果描述串（如 DamageEffect:6）；M4 再解析为效果组件。</summary>
        public IReadOnlyList<string> Effects { get; init; } = Array.Empty<string>();

        public string SetKey { get; init; } = string.Empty;

        public string ArtKey { get; init; } = string.Empty;

        public string AudioKey { get; init; } = string.Empty;

        /// <summary>废弃卡请置 false，不要删行（Id/Key 稳定性，Docs/01 第 7.3 节）。</summary>
        public bool Enabled { get; init; } = true;
    }
}
