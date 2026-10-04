using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>英雄与英雄技能表校验：行解析、Id/Key 唯一、生命/费用范围、技能外键。</summary>
    internal static class HeroConfigValidator
    {
        public static List<HeroPowerDefinition> ValidateHeroPowers(
            CsvTable? table,
            List<ConfigValidationIssue> issues)
        {
            List<HeroPowerDefinition> powers = new List<HeroPowerDefinition>();
            if (!ConfigValidator.RequireTable(table, ConfigValidator.HeroPowersTable, issues))
            {
                return powers;
            }

            HashSet<int> ids = new HashSet<int>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            for (int row = 0; row < table!.RowCount; row++)
            {
                HeroPowerDefinition? power = ParsePower(table, row, issues);
                if (power == null)
                {
                    continue;
                }

                int line = table.GetSourceLineNumber(row);
                ConfigValidator.CheckUnique(
                    issues,
                    ConfigValidator.HeroPowersTable,
                    line,
                    ids,
                    keys,
                    power.Id,
                    power.Key);

                if (power.Cost <= 0)
                {
                    ConfigValidator.Add(
                        issues,
                        ConfigValidator.HeroPowersTable,
                        line,
                        "Cost",
                        "技能费用必须为正，实际为 " + power.Cost);
                }

                powers.Add(power);
            }

            return powers;
        }

        private static HeroPowerDefinition? ParsePower(CsvTable table, int row, List<ConfigValidationIssue> issues)
        {
            try
            {
                return ConfigRowParser.ParseHeroPower(table, row, ConfigValidator.HeroPowersTable);
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
        }

        public static List<HeroDefinition> ValidateHeroes(
            CsvTable? table,
            List<HeroPowerDefinition> powers,
            bool heroPowersTableExists,
            List<ConfigValidationIssue> issues)
        {
            List<HeroDefinition> heroes = new List<HeroDefinition>();
            if (!ConfigValidator.RequireTable(table, ConfigValidator.HeroesTable, issues))
            {
                return heroes;
            }

            HashSet<string> powerKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < powers.Count; i++)
            {
                powerKeys.Add(powers[i].Key);
            }

            HashSet<int> ids = new HashSet<int>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            for (int row = 0; row < table!.RowCount; row++)
            {
                HeroDefinition? hero = ParseHero(table, row, issues);
                if (hero == null)
                {
                    continue;
                }

                int line = table.GetSourceLineNumber(row);
                ConfigValidator.CheckUnique(
                    issues,
                    ConfigValidator.HeroesTable,
                    line,
                    ids,
                    keys,
                    hero.Id,
                    hero.Key);
                CheckHero(hero, line, powerKeys, heroPowersTableExists, issues);
                heroes.Add(hero);
            }

            return heroes;
        }

        private static HeroDefinition? ParseHero(CsvTable table, int row, List<ConfigValidationIssue> issues)
        {
            try
            {
                return ConfigRowParser.ParseHero(table, row, ConfigValidator.HeroesTable);
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
        }

        private static void CheckHero(
            HeroDefinition hero,
            int line,
            HashSet<string> powerKeys,
            bool heroPowersTableExists,
            List<ConfigValidationIssue> issues)
        {
            string table = ConfigValidator.HeroesTable;

            if (hero.Health < 1 || hero.Health > 100)
            {
                ConfigValidator.Add(issues, table, line, "Health", "英雄生命应在 1..100，实际为 " + hero.Health);
            }

            if (heroPowersTableExists && !powerKeys.Contains(hero.HeroPowerKey))
            {
                ConfigValidator.Add(
                    issues,
                    table,
                    line,
                    "HeroPowerKey",
                    "引用了不存在的技能：" + hero.HeroPowerKey);
            }
        }
    }
}
