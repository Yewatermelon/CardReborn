using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>校验通过后的已解析契约集合（导入器直接消费，避免二次解析）。</summary>
    public sealed class ConfigBundle
    {
        public ConfigBundle(
            IReadOnlyList<CardDefinition> cards,
            IReadOnlyList<HeroDefinition> heroes,
            IReadOnlyList<HeroPowerDefinition> heroPowers,
            IReadOnlyList<RarityWeight> rarityWeights,
            GachaConfig gacha,
            RulesConfig rules)
        {
            Cards = Guard.NotNull(cards, nameof(cards));
            Heroes = Guard.NotNull(heroes, nameof(heroes));
            HeroPowers = Guard.NotNull(heroPowers, nameof(heroPowers));
            RarityWeights = Guard.NotNull(rarityWeights, nameof(rarityWeights));
            Gacha = Guard.NotNull(gacha, nameof(gacha));
            Rules = Guard.NotNull(rules, nameof(rules));
        }

        public IReadOnlyList<CardDefinition> Cards { get; }

        public IReadOnlyList<HeroDefinition> Heroes { get; }

        public IReadOnlyList<HeroPowerDefinition> HeroPowers { get; }

        public IReadOnlyList<RarityWeight> RarityWeights { get; }

        public GachaConfig Gacha { get; }

        public RulesConfig Rules { get; }
    }
}
