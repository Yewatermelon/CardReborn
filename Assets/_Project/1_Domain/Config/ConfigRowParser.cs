using System;
using System.Collections.Generic;
using System.Globalization;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// CSV 行到配置契约的映射（Docs/03 第 9.1 节：源表 → 生成物）。
    /// 解析失败一律抛 ConfigFormatException，消息含表名/行号/列名；
    /// 业务级规则（范围、唯一性、外键）交给 M2-T4 的校验器汇总报告，不在这里提前拒绝。
    /// </summary>
    public static class ConfigRowParser
    {
        public static CardDefinition ParseCard(CsvTable table, int rowIndex, string tableName)
        {
            return new CardDefinition
            {
                Id = RequiredInt(table, rowIndex, tableName, "Id"),
                Key = RequiredText(table, rowIndex, tableName, "Key"),
                NameKey = RequiredText(table, rowIndex, tableName, "NameKey"),
                DescKey = RequiredText(table, rowIndex, tableName, "DescKey"),
                Cost = RequiredInt(table, rowIndex, tableName, "Cost"),
                Type = RequiredEnum<CardType>(table, rowIndex, tableName, "Type"),
                Rarity = RequiredEnum<CardRarity>(table, rowIndex, tableName, "Rarity"),
                Class = RequiredEnum<CardClass>(table, rowIndex, tableName, "Class"),
                Attack = OptionalInt(table, rowIndex, tableName, "Attack"),
                Health = OptionalInt(table, rowIndex, tableName, "Health"),
                Keywords = RequiredKeywords(table, rowIndex, tableName),
                TargetRule = RequiredEnum<TargetRule>(table, rowIndex, tableName, "TargetRule"),
                Effects = OptionalList(table, rowIndex, tableName, "Effects"),
                SetKey = RequiredText(table, rowIndex, tableName, "SetKey"),
                ArtKey = OptionalText(table, rowIndex, tableName, "ArtKey"),
                AudioKey = OptionalText(table, rowIndex, tableName, "AudioKey"),
                Enabled = RequiredBool(table, rowIndex, tableName, "Enabled")
            };
        }

        public static HeroDefinition ParseHero(CsvTable table, int rowIndex, string tableName)
        {
            return new HeroDefinition
            {
                Id = RequiredInt(table, rowIndex, tableName, "Id"),
                Key = RequiredText(table, rowIndex, tableName, "Key"),
                NameKey = RequiredText(table, rowIndex, tableName, "NameKey"),
                Health = RequiredInt(table, rowIndex, tableName, "Health"),
                HeroPowerKey = RequiredText(table, rowIndex, tableName, "HeroPowerKey"),
                Class = RequiredEnum<CardClass>(table, rowIndex, tableName, "Class")
            };
        }

        public static HeroPowerDefinition ParseHeroPower(CsvTable table, int rowIndex, string tableName)
        {
            return new HeroPowerDefinition
            {
                Id = RequiredInt(table, rowIndex, tableName, "Id"),
                Key = RequiredText(table, rowIndex, tableName, "Key"),
                Cost = RequiredInt(table, rowIndex, tableName, "Cost"),
                TargetRule = RequiredEnum<TargetRule>(table, rowIndex, tableName, "TargetRule"),
                Effects = OptionalList(table, rowIndex, tableName, "Effects")
            };
        }

        public static RarityWeight ParseRarityWeight(CsvTable table, int rowIndex, string tableName)
        {
            return new RarityWeight
            {
                Rarity = RequiredEnum<CardRarity>(table, rowIndex, tableName, "Rarity"),
                Weight = RequiredInt(table, rowIndex, tableName, "Weight"),
                MinPerPack = RequiredInt(table, rowIndex, tableName, "MinPerPack")
            };
        }

        public static GachaConfig ParseGachaConfig(CsvTable table, int rowIndex, string tableName)
        {
            return new GachaConfig
            {
                PackSize = RequiredInt(table, rowIndex, tableName, "PackSize"),
                CoinCost = RequiredInt(table, rowIndex, tableName, "CoinCost"),
                PityCount = RequiredInt(table, rowIndex, tableName, "PityCount"),
                PityRarity = RequiredEnum<CardRarity>(table, rowIndex, tableName, "PityRarity")
            };
        }

        public static RulesConfig ParseRules(CsvTable table, int rowIndex, string tableName)
        {
            RulesConfig fallback = new RulesConfig();

            return new RulesConfig
            {
                HeroHealth = RequiredInt(table, rowIndex, tableName, "HeroHealth"),
                HandLimit = RequiredInt(table, rowIndex, tableName, "HandLimit"),
                BoardLimit = RequiredInt(table, rowIndex, tableName, "BoardLimit"),
                ManaLimit = RequiredInt(table, rowIndex, tableName, "ManaLimit"),
                DeckSize = OptionalIntOrDefault(table, rowIndex, tableName, "DeckSize", fallback.DeckSize),
                StartingHandFirst = OptionalIntOrDefault(
                    table, rowIndex, tableName, "StartingHandFirst", fallback.StartingHandFirst),
                StartingHandSecond = OptionalIntOrDefault(
                    table, rowIndex, tableName, "StartingHandSecond", fallback.StartingHandSecond),
                TheCoinCardKey = OptionalTextOrDefault(
                    table, rowIndex, "TheCoinCardKey", fallback.TheCoinCardKey)
            };
        }

        private static string Cell(CsvTable table, int rowIndex, string tableName, string column)
        {
            if (!table.HasColumn(column))
            {
                throw new ConfigFormatException(tableName, 1, column, "表头缺少该列");
            }

            return table.GetCell(rowIndex, column);
        }

        private static string RequiredText(CsvTable table, int rowIndex, string tableName, string column)
        {
            string value = Cell(table, rowIndex, tableName, column);
            if (value.Length == 0)
            {
                throw new ConfigFormatException(tableName, Line(table, rowIndex), column, "不能为空");
            }

            return value;
        }

        private static string OptionalText(CsvTable table, int rowIndex, string tableName, string column)
        {
            return Cell(table, rowIndex, tableName, column);
        }

        private static bool RequiredBool(CsvTable table, int rowIndex, string tableName, string column)
        {
            string value = RequiredText(table, rowIndex, tableName, column);
            if (value == "TRUE" || value == "true" || value == "1")
            {
                return true;
            }

            if (value == "FALSE" || value == "false" || value == "0")
            {
                return false;
            }

            throw new ConfigFormatException(
                tableName,
                Line(table, rowIndex),
                column,
                "只接受 TRUE/FALSE/1/0，实际为 '" + value + "'");
        }

        private static int RequiredInt(CsvTable table, int rowIndex, string tableName, string column)
        {
            return ParseInt(RequiredText(table, rowIndex, tableName, column), table, rowIndex, tableName, column);
        }

        private static int OptionalInt(CsvTable table, int rowIndex, string tableName, string column)
        {
            string value = Cell(table, rowIndex, tableName, column);
            return value.Length == 0 ? 0 : ParseInt(value, table, rowIndex, tableName, column);
        }

        /// <summary>缺列或空单元格时回落 <paramref name="fallback"/>；有值则严格解析。</summary>
        private static int OptionalIntOrDefault(
            CsvTable table, int rowIndex, string tableName, string column, int fallback)
        {
            if (!table.HasColumn(column))
            {
                return fallback;
            }

            string value = table.GetCell(rowIndex, column);
            return value.Length == 0
                ? fallback
                : ParseInt(value, table, rowIndex, tableName, column);
        }

        /// <summary>缺列或空单元格时回落 <paramref name="fallback"/>（可为 null）。</summary>
        private static string? OptionalTextOrDefault(
            CsvTable table, int rowIndex, string column, string? fallback)
        {
            if (!table.HasColumn(column))
            {
                return fallback;
            }

            string value = table.GetCell(rowIndex, column);
            return value.Length == 0 ? fallback : value;
        }

        private static int ParseInt(string value, CsvTable table, int rowIndex, string tableName, string column)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ConfigFormatException(
                    tableName,
                    Line(table, rowIndex),
                    column,
                    "必须是整数，实际为 '" + value + "'");
            }

            return parsed;
        }

        private static TEnum RequiredEnum<TEnum>(CsvTable table, int rowIndex, string tableName, string column)
            where TEnum : struct, Enum
        {
            string value = RequiredText(table, rowIndex, tableName, column);
            if (!ConfigTokens.TryParseEnum(value, out TEnum parsed))
            {
                throw new ConfigFormatException(
                    tableName,
                    Line(table, rowIndex),
                    column,
                    "不是合法枚举值（禁止写数字），实际为 '" + value + "'");
            }

            return parsed;
        }

        private static Keyword RequiredKeywords(CsvTable table, int rowIndex, string tableName)
        {
            string value = Cell(table, rowIndex, tableName, "Keywords");
            if (!KeywordTokens.TryParse(value, out Keyword keywords, out string? failedToken))
            {
                throw new ConfigFormatException(
                    tableName,
                    Line(table, rowIndex),
                    "Keywords",
                    "含未知关键词 '" + failedToken + "'");
            }

            return keywords;
        }

        private static IReadOnlyList<string> OptionalList(CsvTable table, int rowIndex, string tableName, string column)
        {
            string value = Cell(table, rowIndex, tableName, column);
            if (value.Length == 0)
            {
                return Array.Empty<string>();
            }

            string[] parts = value.Split(KeywordTokens.Separator);
            List<string> items = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0)
                {
                    items.Add(part);
                }
            }

            return items;
        }

        private static int Line(CsvTable table, int rowIndex)
        {
            return table.GetSourceLineNumber(rowIndex);
        }
    }
}
