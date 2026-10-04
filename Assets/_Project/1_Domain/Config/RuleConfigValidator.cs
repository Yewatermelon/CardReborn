using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>稀有度权重、抽卡与全局规则表的校验。</summary>
    internal static class RuleConfigValidator
    {
        public static List<RarityWeight> ValidateRarityWeights(
            CsvTable? table,
            List<ConfigValidationIssue> issues)
        {
            List<RarityWeight> weights = new List<RarityWeight>();
            if (!ConfigValidator.RequireTable(table, ConfigValidator.RarityWeightsTable, issues))
            {
                return weights;
            }

            HashSet<CardRarity> seen = new HashSet<CardRarity>();
            for (int row = 0; row < table!.RowCount; row++)
            {
                RarityWeight weight;
                try
                {
                    weight = ConfigRowParser.ParseRarityWeight(table, row, ConfigValidator.RarityWeightsTable);
                }
                catch (ConfigFormatException exception)
                {
                    ConfigValidator.Add(
                        issues,
                        exception.TableName,
                        exception.LineNumber,
                        exception.ColumnName,
                        exception.Reason);
                    continue;
                }

                CheckWeight(weight, table.GetSourceLineNumber(row), seen, issues);
                weights.Add(weight);
            }

            EnsureAllRaritiesSeen(seen, issues);
            return weights;
        }

        public static GachaConfig? ValidateGacha(CsvTable? table, List<ConfigValidationIssue> issues)
        {
            if (!ConfigValidator.RequireTable(table, ConfigValidator.GachaConfigTable, issues) ||
                !ConfigValidator.RequireSingleRow(table!, ConfigValidator.GachaConfigTable, issues))
            {
                return null;
            }

            GachaConfig gacha;
            try
            {
                gacha = ConfigRowParser.ParseGachaConfig(table!, 0, ConfigValidator.GachaConfigTable);
            }
            catch (ConfigFormatException exception)
            {
                ConfigValidator.Add(
                    issues,
                    exception.TableName,
                    exception.LineNumber,
                    exception.ColumnName,
                    exception.Reason);
                return null;
            }

            int line = table!.GetSourceLineNumber(0);
            CheckPositive(issues, ConfigValidator.GachaConfigTable, line, "PackSize", gacha.PackSize, "每包张数");
            CheckPositive(issues, ConfigValidator.GachaConfigTable, line, "CoinCost", gacha.CoinCost, "单包金币消耗");
            CheckPositive(issues, ConfigValidator.GachaConfigTable, line, "PityCount", gacha.PityCount, "保底包数");
            return gacha;
        }

        public static RulesConfig? ValidateRules(CsvTable? table, List<ConfigValidationIssue> issues)
        {
            if (!ConfigValidator.RequireTable(table, ConfigValidator.RulesTable, issues) ||
                !ConfigValidator.RequireSingleRow(table!, ConfigValidator.RulesTable, issues))
            {
                return null;
            }

            RulesConfig rules;
            try
            {
                rules = ConfigRowParser.ParseRules(table!, 0, ConfigValidator.RulesTable);
            }
            catch (ConfigFormatException exception)
            {
                ConfigValidator.Add(
                    issues,
                    exception.TableName,
                    exception.LineNumber,
                    exception.ColumnName,
                    exception.Reason);
                return null;
            }

            int line = table!.GetSourceLineNumber(0);
            CheckRange(issues, line, "HeroHealth", rules.HeroHealth, 1, 100);
            CheckRange(issues, line, "HandLimit", rules.HandLimit, 1, 10);
            CheckRange(issues, line, "BoardLimit", rules.BoardLimit, 1, 7);
            CheckRange(issues, line, "ManaLimit", rules.ManaLimit, 1, 10);
            return rules;
        }

        private static void CheckWeight(
            RarityWeight weight,
            int line,
            HashSet<CardRarity> seen,
            List<ConfigValidationIssue> issues)
        {
            string table = ConfigValidator.RarityWeightsTable;

            if (!seen.Add(weight.Rarity))
            {
                ConfigValidator.Add(issues, table, line, "Rarity", "稀有度重复：" + weight.Rarity);
            }

            if (weight.Weight <= 0)
            {
                ConfigValidator.Add(issues, table, line, "Weight", "权重必须为正，实际为 " + weight.Weight);
            }

            if (weight.MinPerPack < 0)
            {
                ConfigValidator.Add(issues, table, line, "MinPerPack", "不能为负数，实际为 " + weight.MinPerPack);
            }
        }

        private static void EnsureAllRaritiesSeen(HashSet<CardRarity> seen, List<ConfigValidationIssue> issues)
        {
            CardRarity[] all = (CardRarity[])Enum.GetValues(typeof(CardRarity));
            for (int i = 0; i < all.Length; i++)
            {
                if (!seen.Contains(all[i]))
                {
                    ConfigValidator.Add(
                        issues,
                        ConfigValidator.RarityWeightsTable,
                        0,
                        "Rarity",
                        "缺少稀有度配置：" + all[i]);
                }
            }
        }

        private static void CheckPositive(
            List<ConfigValidationIssue> issues,
            string table,
            int line,
            string column,
            int value,
            string label)
        {
            if (value <= 0)
            {
                ConfigValidator.Add(issues, table, line, column, label + "必须为正，实际为 " + value);
            }
        }

        private static void CheckRange(
            List<ConfigValidationIssue> issues,
            int line,
            string column,
            int value,
            int min,
            int max)
        {
            if (value < min || value > max)
            {
                ConfigValidator.Add(
                    issues,
                    ConfigValidator.RulesTable,
                    line,
                    column,
                    "应在 " + min + ".." + max + "，实际为 " + value);
            }
        }
    }
}
