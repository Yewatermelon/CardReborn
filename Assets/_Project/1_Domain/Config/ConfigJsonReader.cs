using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 生成物 JSON → 配置契约（导入器写出方向的逆运算）。
    /// 严格读取：字段缺失、类型不符、schema 版本不匹配都会抛 <see cref="ConfigReadException"/>（带文件与 JSON 路径）。
    /// 说明：Docs/03 第 9.1 节提到版本不匹配时"警告并尝试迁移"；当前**不做自动迁移**，直接失败（登记为 M2-R3），
    /// 避免把不同版本的字段静默读成错值。
    /// </summary>
    public static class ConfigJsonReader
    {
        public static IReadOnlyList<CardDefinition> ReadCards(JsonValue document, string source)
        {
            RequireSchemaVersion(document, source);
            IReadOnlyList<JsonValue> items = RequireArray(document, "cards", source);

            List<CardDefinition> cards = new List<CardDefinition>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                string path = "cards[" + i + "]";
                cards.Add(new CardDefinition
                {
                    Id = RequireInt(items[i], "id", source, path),
                    Key = RequireString(items[i], "key", source, path),
                    NameKey = RequireString(items[i], "nameKey", source, path),
                    DescKey = RequireString(items[i], "descKey", source, path),
                    Cost = RequireInt(items[i], "cost", source, path),
                    Type = RequireEnum<CardType>(items[i], "type", source, path),
                    Rarity = RequireEnum<CardRarity>(items[i], "rarity", source, path),
                    Class = RequireEnum<CardClass>(items[i], "class", source, path),
                    Attack = RequireInt(items[i], "attack", source, path),
                    Health = RequireInt(items[i], "health", source, path),
                    Keywords = RequireKeywords(items[i], source, path),
                    TargetRule = RequireEnum<TargetRule>(items[i], "targetRule", source, path),
                    Effects = RequireStringArray(items[i], "effects", source, path),
                    SetKey = RequireString(items[i], "setKey", source, path),
                    ArtKey = RequireString(items[i], "artKey", source, path),
                    AudioKey = RequireString(items[i], "audioKey", source, path),
                    Enabled = RequireBool(items[i], "enabled", source, path)
                });
            }

            return cards;
        }

        public static IReadOnlyList<HeroDefinition> ReadHeroes(JsonValue document, string source)
        {
            RequireSchemaVersion(document, source);
            IReadOnlyList<JsonValue> items = RequireArray(document, "heroes", source);

            List<HeroDefinition> heroes = new List<HeroDefinition>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                string path = "heroes[" + i + "]";
                heroes.Add(new HeroDefinition
                {
                    Id = RequireInt(items[i], "id", source, path),
                    Key = RequireString(items[i], "key", source, path),
                    NameKey = RequireString(items[i], "nameKey", source, path),
                    Health = RequireInt(items[i], "health", source, path),
                    HeroPowerKey = RequireString(items[i], "heroPowerKey", source, path),
                    Class = RequireEnum<CardClass>(items[i], "class", source, path)
                });
            }

            return heroes;
        }

        public static IReadOnlyList<HeroPowerDefinition> ReadHeroPowers(JsonValue document, string source)
        {
            RequireSchemaVersion(document, source);
            IReadOnlyList<JsonValue> items = RequireArray(document, "heroPowers", source);

            List<HeroPowerDefinition> powers = new List<HeroPowerDefinition>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                string path = "heroPowers[" + i + "]";
                powers.Add(new HeroPowerDefinition
                {
                    Id = RequireInt(items[i], "id", source, path),
                    Key = RequireString(items[i], "key", source, path),
                    Cost = RequireInt(items[i], "cost", source, path),
                    TargetRule = RequireEnum<TargetRule>(items[i], "targetRule", source, path),
                    Effects = RequireStringArray(items[i], "effects", source, path)
                });
            }

            return powers;
        }

        public static IReadOnlyList<RarityWeight> ReadRarityWeights(JsonValue document, string source)
        {
            RequireSchemaVersion(document, source);
            IReadOnlyList<JsonValue> items = RequireArray(document, "rarityWeights", source);

            List<RarityWeight> weights = new List<RarityWeight>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                string path = "rarityWeights[" + i + "]";
                weights.Add(new RarityWeight
                {
                    Rarity = RequireEnum<CardRarity>(items[i], "rarity", source, path),
                    Weight = RequireInt(items[i], "weight", source, path),
                    MinPerPack = RequireInt(items[i], "minPerPack", source, path)
                });
            }

            return weights;
        }

        public static GachaConfig ReadGacha(JsonValue document, string source)
        {
            RequireSchemaVersion(document, source);
            JsonValue body = RequireObject(document, "gacha", source);

            return new GachaConfig
            {
                PackSize = RequireInt(body, "packSize", source, "gacha"),
                CoinCost = RequireInt(body, "coinCost", source, "gacha"),
                PityCount = RequireInt(body, "pityCount", source, "gacha"),
                PityRarity = RequireEnum<CardRarity>(body, "pityRarity", source, "gacha")
            };
        }

        public static RulesConfig ReadRules(JsonValue document, string source)
        {
            RequireSchemaVersion(document, source);
            JsonValue body = RequireObject(document, "rules", source);

            return new RulesConfig
            {
                HeroHealth = RequireInt(body, "heroHealth", source, "rules"),
                HandLimit = RequireInt(body, "handLimit", source, "rules"),
                BoardLimit = RequireInt(body, "boardLimit", source, "rules"),
                ManaLimit = RequireInt(body, "manaLimit", source, "rules")
            };
        }

        /// <summary>一次读齐六份文档；任一失败即抛异常（调用方决定如何汇总）。</summary>
        public static ConfigBundle ReadBundle(
            JsonValue cards,
            JsonValue heroes,
            JsonValue heroPowers,
            JsonValue rarityWeights,
            JsonValue gacha,
            JsonValue rules)
        {
            return new ConfigBundle(
                ReadCards(cards, ConfigSchema.CardsFileName),
                ReadHeroes(heroes, ConfigSchema.HeroesFileName),
                ReadHeroPowers(heroPowers, ConfigSchema.HeroPowersFileName),
                ReadRarityWeights(rarityWeights, ConfigSchema.RarityWeightsFileName),
                ReadGacha(gacha, ConfigSchema.GachaFileName),
                ReadRules(rules, ConfigSchema.RulesFileName));
        }

        private static void RequireSchemaVersion(JsonValue document, string source)
        {
            if (document.Kind != JsonKind.Object)
            {
                throw new ConfigReadException(source, "<root>", "顶层必须是对象");
            }

            int version = RequireInt(document, ConfigSchema.VersionProperty, source, "<root>");
            if (version != ConfigSchema.CurrentVersion)
            {
                throw new ConfigReadException(
                    source,
                    ConfigSchema.VersionProperty,
                    "schema 版本不匹配（期望 " + ConfigSchema.CurrentVersion + "，实际 " + version + "）；本版本不做自动迁移");
            }
        }

        private static IReadOnlyList<JsonValue> RequireArray(JsonValue document, string name, string source)
        {
            JsonValue value = RequireMember(document, name, source, "<root>");
            if (value.Kind != JsonKind.Array)
            {
                throw new ConfigReadException(source, name, "应为数组，实际为 " + value.Kind);
            }

            return value.Items;
        }

        private static JsonValue RequireObject(JsonValue document, string name, string source)
        {
            JsonValue value = RequireMember(document, name, source, "<root>");
            if (value.Kind != JsonKind.Object)
            {
                throw new ConfigReadException(source, name, "应为对象，实际为 " + value.Kind);
            }

            return value;
        }

        private static JsonValue RequireMember(JsonValue value, string name, string source, string path)
        {
            if (value.Kind != JsonKind.Object || !value.TryGetMember(name, out JsonValue member))
            {
                throw new ConfigReadException(source, path, "缺少字段 " + name);
            }

            return member;
        }

        private static int RequireInt(JsonValue value, string name, string source, string path)
        {
            JsonValue member = RequireMember(value, name, source, path);
            if (member.Kind != JsonKind.Number)
            {
                throw new ConfigReadException(source, path + "." + name, "应为整数，实际为 " + member.Kind);
            }

            return member.IntValue;
        }

        private static string RequireString(JsonValue value, string name, string source, string path)
        {
            JsonValue member = RequireMember(value, name, source, path);
            if (member.Kind != JsonKind.String)
            {
                throw new ConfigReadException(source, path + "." + name, "应为字符串，实际为 " + member.Kind);
            }

            return member.StringValue;
        }

        private static bool RequireBool(JsonValue value, string name, string source, string path)
        {
            JsonValue member = RequireMember(value, name, source, path);
            if (member.Kind != JsonKind.Bool)
            {
                throw new ConfigReadException(source, path + "." + name, "应为布尔，实际为 " + member.Kind);
            }

            return member.BoolValue;
        }

        private static TEnum RequireEnum<TEnum>(JsonValue value, string name, string source, string path)
            where TEnum : struct, Enum
        {
            string text = RequireString(value, name, source, path);
            if (!ConfigTokens.TryParseEnum(text, out TEnum parsed))
            {
                throw new ConfigReadException(source, path + "." + name, "不是合法枚举值：" + text);
            }

            return parsed;
        }

        private static Keyword RequireKeywords(JsonValue value, string source, string path)
        {
            JsonValue member = RequireMember(value, "keywords", source, path);
            if (member.Kind != JsonKind.String)
            {
                throw new ConfigReadException(source, path + ".keywords", "应为字符串（Taunt|Charge），实际为 " + member.Kind);
            }

            if (!KeywordTokens.TryParse(member.StringValue, out Keyword keywords, out string? failedToken))
            {
                throw new ConfigReadException(source, path + ".keywords", "含未知关键词：" + failedToken);
            }

            return keywords;
        }

        private static IReadOnlyList<string> RequireStringArray(JsonValue value, string name, string source, string path)
        {
            JsonValue member = RequireMember(value, name, source, path);
            if (member.Kind != JsonKind.Array)
            {
                throw new ConfigReadException(source, path + "." + name, "应为数组，实际为 " + member.Kind);
            }

            List<string> items = new List<string>(member.Items.Count);
            for (int i = 0; i < member.Items.Count; i++)
            {
                if (member.Items[i].Kind != JsonKind.String)
                {
                    throw new ConfigReadException(source, path + "." + name + "[" + i + "]", "应为字符串");
                }

                items.Add(member.Items[i].StringValue);
            }

            return items;
        }
    }
}
