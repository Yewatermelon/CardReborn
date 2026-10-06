using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// ConfigJsonReader 的 partial 分文件（主文件 ≤300 行）：抽卡配置读取，
    /// 以及 Rules 表追加字段（M3-T5）的"缺失回落默认"读取原语。
    /// </summary>
    public static partial class ConfigJsonReader
    {
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

        /// <summary>字段缺失时回落默认；字段存在但类型错误仍严格失败（防静默读错值）。</summary>
        private static int OptionalInt(JsonValue value, string name, int fallback, string source)
        {
            if (value.Kind != JsonKind.Object || !value.TryGetMember(name, out JsonValue member))
            {
                return fallback;
            }

            if (member.Kind != JsonKind.Number)
            {
                throw new ConfigReadException(source, "rules." + name, "应为整数，实际为 " + member.Kind);
            }

            return member.IntValue;
        }

        /// <summary>字段缺失或为 null 时回落默认；字段存在但类型错误仍严格失败。</summary>
        private static string? OptionalString(JsonValue value, string name, string? fallback, string source)
        {
            if (value.Kind != JsonKind.Object || !value.TryGetMember(name, out JsonValue member))
            {
                return fallback;
            }

            if (member.Kind == JsonKind.Null)
            {
                return null;
            }

            if (member.Kind != JsonKind.String)
            {
                throw new ConfigReadException(source, "rules." + name, "应为字符串，实际为 " + member.Kind);
            }

            return member.StringValue;
        }
    }
}
