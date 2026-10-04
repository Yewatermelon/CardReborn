using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 配置契约 → JSON 文档（导入器的后半段）。
    /// 约定：
    /// 1. 顶层信封固定为 <c>{ "schemaVersion": N, "&lt;entriesName&gt;": … }</c>（Docs/03 第 9.1 节）；
    /// 2. 成员名用 camelCase；枚举写成名字字符串；关键词沿用 <c>Taunt|Charge</c> 文本（与源表一致，便于人工核对）；
    /// 3. 输出顺序固定 → 生成物可 diff（具体格式由 JsonValue 保证）。
    /// </summary>
    public static class ConfigJsonWriter
    {
        public static JsonValue WriteCards(IReadOnlyList<CardDefinition> cards)
        {
            Guard.NotNull(cards, nameof(cards));

            JsonValue[] items = new JsonValue[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition card = cards[i];
                items[i] = JsonValue.Object()
                    .Add("id", JsonValue.From(card.Id))
                    .Add("key", JsonValue.From(card.Key))
                    .Add("nameKey", JsonValue.From(card.NameKey))
                    .Add("descKey", JsonValue.From(card.DescKey))
                    .Add("cost", JsonValue.From(card.Cost))
                    .Add("type", JsonValue.From(card.Type.ToString()))
                    .Add("rarity", JsonValue.From(card.Rarity.ToString()))
                    .Add("class", JsonValue.From(card.Class.ToString()))
                    .Add("attack", JsonValue.From(card.Attack))
                    .Add("health", JsonValue.From(card.Health))
                    .Add("keywords", JsonValue.From(KeywordTokens.Format(card.Keywords)))
                    .Add("targetRule", JsonValue.From(card.TargetRule.ToString()))
                    .Add("effects", ToJsonArray(card.Effects))
                    .Add("setKey", JsonValue.From(card.SetKey))
                    .Add("artKey", JsonValue.From(card.ArtKey))
                    .Add("audioKey", JsonValue.From(card.AudioKey))
                    .Add("enabled", JsonValue.From(card.Enabled));
            }

            return Envelope("cards", JsonValue.Array(items));
        }

        public static JsonValue WriteHeroes(IReadOnlyList<HeroDefinition> heroes)
        {
            Guard.NotNull(heroes, nameof(heroes));

            JsonValue[] items = new JsonValue[heroes.Count];
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroDefinition hero = heroes[i];
                items[i] = JsonValue.Object()
                    .Add("id", JsonValue.From(hero.Id))
                    .Add("key", JsonValue.From(hero.Key))
                    .Add("nameKey", JsonValue.From(hero.NameKey))
                    .Add("health", JsonValue.From(hero.Health))
                    .Add("heroPowerKey", JsonValue.From(hero.HeroPowerKey))
                    .Add("class", JsonValue.From(hero.Class.ToString()));
            }

            return Envelope("heroes", JsonValue.Array(items));
        }

        public static JsonValue WriteHeroPowers(IReadOnlyList<HeroPowerDefinition> powers)
        {
            Guard.NotNull(powers, nameof(powers));

            JsonValue[] items = new JsonValue[powers.Count];
            for (int i = 0; i < powers.Count; i++)
            {
                HeroPowerDefinition power = powers[i];
                items[i] = JsonValue.Object()
                    .Add("id", JsonValue.From(power.Id))
                    .Add("key", JsonValue.From(power.Key))
                    .Add("cost", JsonValue.From(power.Cost))
                    .Add("targetRule", JsonValue.From(power.TargetRule.ToString()))
                    .Add("effects", ToJsonArray(power.Effects));
            }

            return Envelope("heroPowers", JsonValue.Array(items));
        }

        public static JsonValue WriteRarityWeights(IReadOnlyList<RarityWeight> weights)
        {
            Guard.NotNull(weights, nameof(weights));

            JsonValue[] items = new JsonValue[weights.Count];
            for (int i = 0; i < weights.Count; i++)
            {
                RarityWeight weight = weights[i];
                items[i] = JsonValue.Object()
                    .Add("rarity", JsonValue.From(weight.Rarity.ToString()))
                    .Add("weight", JsonValue.From(weight.Weight))
                    .Add("minPerPack", JsonValue.From(weight.MinPerPack));
            }

            return Envelope("rarityWeights", JsonValue.Array(items));
        }

        public static JsonValue WriteGacha(GachaConfig config)
        {
            Guard.NotNull(config, nameof(config));

            JsonValue gacha = JsonValue.Object()
                .Add("packSize", JsonValue.From(config.PackSize))
                .Add("coinCost", JsonValue.From(config.CoinCost))
                .Add("pityCount", JsonValue.From(config.PityCount))
                .Add("pityRarity", JsonValue.From(config.PityRarity.ToString()));

            return Envelope("gacha", gacha);
        }

        public static JsonValue WriteRules(RulesConfig rules)
        {
            Guard.NotNull(rules, nameof(rules));

            JsonValue body = JsonValue.Object()
                .Add("heroHealth", JsonValue.From(rules.HeroHealth))
                .Add("handLimit", JsonValue.From(rules.HandLimit))
                .Add("boardLimit", JsonValue.From(rules.BoardLimit))
                .Add("manaLimit", JsonValue.From(rules.ManaLimit));

            return Envelope("rules", body);
        }

        private static JsonValue Envelope(string entriesName, JsonValue entries)
        {
            return JsonValue.Object()
                .Add("_generated", JsonValue.From("由 Tools/Card/导入配置 生成，请勿手改"))
                .Add(ConfigSchema.VersionProperty, JsonValue.From(ConfigSchema.CurrentVersion))
                .Add(entriesName, entries);
        }

        private static JsonValue ToJsonArray(IReadOnlyList<string> values)
        {
            JsonValue[] items = new JsonValue[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                items[i] = JsonValue.From(values[i]);
            }

            return JsonValue.Array(items);
        }
    }
}
