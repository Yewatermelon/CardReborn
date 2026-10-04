using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 卡池查询服务（FR-1.4 / FR-1.5）：id / key O(1) 查询，按职业、稀有度、系列筛选。
    /// 约定：
    /// 1. 查不到时 Try* 返回 false、Require* 抛 ConfigLookupException（带 id/key），绝不返回 null 让调用方崩；
    /// 2. 只持有数据、不碰文件系统（读盘在 Infrastructure 的 ConfigFileLoader），内核与服务端共用同一份实现；
    /// 3. 构造时发现重复 id/key 直接抛错——生成物被手改过时立刻暴露。
    /// </summary>
    public sealed class CardDatabase
    {
        private readonly Dictionary<int, CardDefinition> _cardsById = new Dictionary<int, CardDefinition>();
        private readonly Dictionary<string, CardDefinition> _cardsByKey =
            new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroDefinition> _heroesByKey =
            new Dictionary<string, HeroDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroPowerDefinition> _heroPowersByKey =
            new Dictionary<string, HeroPowerDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<CardRarity, RarityWeight> _rarityWeights =
            new Dictionary<CardRarity, RarityWeight>();
        private readonly List<CardDefinition> _allCards = new List<CardDefinition>();
        private readonly List<CardDefinition> _enabledCards = new List<CardDefinition>();

        /// <summary>用已校验的配置集合建库。</summary>
        public CardDatabase(ConfigBundle bundle)
        {
            Guard.NotNull(bundle, nameof(bundle));

            Gacha = Guard.NotNull(bundle.Gacha, nameof(bundle));
            Rules = Guard.NotNull(bundle.Rules, nameof(bundle));
            IndexCards(bundle.Cards);
            IndexHeroes(bundle.Heroes, bundle.HeroPowers);
            IndexRarityWeights(bundle.RarityWeights);
        }

        /// <summary>抽卡与保底配置。</summary>
        public GachaConfig Gacha { get; }

        /// <summary>全局规则数值。</summary>
        public RulesConfig Rules { get; }

        /// <summary>卡牌总数（含废弃卡）。</summary>
        public int CardCount
        {
            get { return _allCards.Count; }
        }

        /// <summary>启用中的卡牌数量。</summary>
        public int EnabledCardCount
        {
            get { return _enabledCards.Count; }
        }

        public int HeroCount
        {
            get { return _heroesByKey.Count; }
        }

        /// <summary>全部卡牌（含废弃卡，顺序与源表一致）。</summary>
        public IReadOnlyList<CardDefinition> AllCards
        {
            get { return _allCards; }
        }

        public bool TryGetCard(int id, out CardDefinition? card)
        {
            return _cardsById.TryGetValue(id, out card);
        }

        public bool TryGetCard(string key, out CardDefinition? card)
        {
            Guard.NotNull(key, nameof(key));
            return _cardsByKey.TryGetValue(key, out card);
        }

        /// <summary>按 id 取卡；不存在抛 ConfigLookupException。</summary>
        public CardDefinition RequireCard(int id)
        {
            if (_cardsById.TryGetValue(id, out CardDefinition? card))
            {
                return card!;
            }

            throw new ConfigLookupException("找不到卡牌 id=" + id);
        }

        /// <summary>按 key 取卡；不存在抛 ConfigLookupException。</summary>
        public CardDefinition RequireCard(string key)
        {
            Guard.NotNull(key, nameof(key));

            if (_cardsByKey.TryGetValue(key, out CardDefinition? card))
            {
                return card!;
            }

            throw new ConfigLookupException("找不到卡牌 key=" + key);
        }

        /// <summary>按职业 / 稀有度 / 系列筛选（都是可选条件；顺序与源表一致）。</summary>
        public IReadOnlyList<CardDefinition> FilterCards(
            CardClass? cardClass = null,
            CardRarity? rarity = null,
            string? setKey = null,
            bool includeDisabled = false)
        {
            List<CardDefinition> source = includeDisabled ? _allCards : _enabledCards;
            List<CardDefinition> result = new List<CardDefinition>();

            for (int i = 0; i < source.Count; i++)
            {
                CardDefinition card = source[i];

                if (cardClass.HasValue && card.Class != cardClass.Value)
                {
                    continue;
                }

                if (rarity.HasValue && card.Rarity != rarity.Value)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(setKey) && !string.Equals(card.SetKey, setKey, StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(card);
            }

            return result;
        }

        public HeroDefinition RequireHero(string key)
        {
            Guard.NotNull(key, nameof(key));

            if (_heroesByKey.TryGetValue(key, out HeroDefinition? hero))
            {
                return hero!;
            }

            throw new ConfigLookupException("找不到英雄 key=" + key);
        }

        public HeroPowerDefinition RequireHeroPower(string key)
        {
            Guard.NotNull(key, nameof(key));

            if (_heroPowersByKey.TryGetValue(key, out HeroPowerDefinition? power))
            {
                return power!;
            }

            throw new ConfigLookupException("找不到英雄技能 key=" + key);
        }

        /// <summary>取某稀有度的抽卡权重；未配置该档位抛 ConfigLookupException。</summary>
        public RarityWeight GetRarityWeight(CardRarity rarity)
        {
            if (_rarityWeights.TryGetValue(rarity, out RarityWeight? weight))
            {
                return weight!;
            }

            throw new ConfigLookupException("缺少稀有度配置：" + rarity);
        }

        private void IndexCards(IReadOnlyList<CardDefinition> cards)
        {
            Guard.NotNull(cards, nameof(cards));

            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition card = cards[i];
                if (_cardsById.ContainsKey(card.Id))
                {
                    throw new ConfigLookupException("卡牌 Id 重复：" + card.Id);
                }

                if (_cardsByKey.ContainsKey(card.Key))
                {
                    throw new ConfigLookupException("卡牌 Key 重复：" + card.Key);
                }

                _cardsById.Add(card.Id, card);
                _cardsByKey.Add(card.Key, card);
                _allCards.Add(card);

                if (card.Enabled)
                {
                    _enabledCards.Add(card);
                }
            }
        }

        private void IndexHeroes(
            IReadOnlyList<HeroDefinition> heroes,
            IReadOnlyList<HeroPowerDefinition> heroPowers)
        {
            Guard.NotNull(heroes, nameof(heroes));
            Guard.NotNull(heroPowers, nameof(heroPowers));

            for (int i = 0; i < heroPowers.Count; i++)
            {
                HeroPowerDefinition power = heroPowers[i];
                if (_heroPowersByKey.ContainsKey(power.Key))
                {
                    throw new ConfigLookupException("英雄技能 Key 重复：" + power.Key);
                }

                _heroPowersByKey.Add(power.Key, power);
            }

            for (int i = 0; i < heroes.Count; i++)
            {
                HeroDefinition hero = heroes[i];
                if (_heroesByKey.ContainsKey(hero.Key))
                {
                    throw new ConfigLookupException("英雄 Key 重复：" + hero.Key);
                }

                _heroesByKey.Add(hero.Key, hero);
            }
        }

        private void IndexRarityWeights(IReadOnlyList<RarityWeight> weights)
        {
            Guard.NotNull(weights, nameof(weights));

            for (int i = 0; i < weights.Count; i++)
            {
                RarityWeight weight = weights[i];
                if (_rarityWeights.ContainsKey(weight.Rarity))
                {
                    throw new ConfigLookupException("稀有度重复：" + weight.Rarity);
                }

                _rarityWeights.Add(weight.Rarity, weight);
            }
        }
    }
}
