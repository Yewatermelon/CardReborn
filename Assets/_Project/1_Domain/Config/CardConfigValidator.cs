using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>卡牌表校验：行解析、Id/Key 唯一、费用与战斗数值范围、非随从不得带战斗数值。</summary>
    internal static class CardConfigValidator
    {
        public static List<CardDefinition> ValidateCards(
            CsvTable? table,
            RulesConfig? rules,
            List<ConfigValidationIssue> issues)
        {
            List<CardDefinition> cards = new List<CardDefinition>();
            if (!ConfigValidator.RequireTable(table, ConfigValidator.CardsTable, issues))
            {
                return cards;
            }

            HashSet<int> ids = new HashSet<int>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            for (int row = 0; row < table!.RowCount; row++)
            {
                CardDefinition? card = ParseCard(table, row, issues);
                if (card == null)
                {
                    continue;
                }

                int line = table.GetSourceLineNumber(row);
                ConfigValidator.CheckUnique(
                    issues,
                    ConfigValidator.CardsTable,
                    line,
                    ids,
                    keys,
                    card.Id,
                    card.Key);

                if (card.Enabled)
                {
                    ValidateCardRanges(card, line, rules, issues);
                }

                cards.Add(card);
            }

            return cards;
        }

        private static CardDefinition? ParseCard(CsvTable table, int row, List<ConfigValidationIssue> issues)
        {
            try
            {
                return ConfigRowParser.ParseCard(table, row, ConfigValidator.CardsTable);
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

        private static void ValidateCardRanges(
            CardDefinition card,
            int line,
            RulesConfig? rules,
            List<ConfigValidationIssue> issues)
        {
            string table = ConfigValidator.CardsTable;

            if (card.Cost < 0)
            {
                ConfigValidator.Add(issues, table, line, "Cost", "费用不能为负数，实际为 " + card.Cost);
            }

            if (rules != null && card.Cost > rules.ManaLimit)
            {
                ConfigValidator.Add(
                    issues,
                    table,
                    line,
                    "Cost",
                    "费用超过规则表 ManaLimit=" + rules.ManaLimit);
            }

            if (card.Type == CardType.Minion)
            {
                if (card.Attack < 0)
                {
                    ConfigValidator.Add(issues, table, line, "Attack", "攻击力不能为负数，实际为 " + card.Attack);
                }

                if (card.Health < 1)
                {
                    ConfigValidator.Add(issues, table, line, "Health", "随从生命必须 ≥ 1，实际为 " + card.Health);
                }

                return;
            }

            if (card.Attack != 0 || card.Health != 0)
            {
                ConfigValidator.Add(
                    issues,
                    table,
                    line,
                    "Attack",
                    "非随从不应有攻击/生命（实际 " + card.Attack + "/" + card.Health + "）");
            }
        }
    }
}
