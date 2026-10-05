using Card.Domain.Config;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T2 测试辅助：构造最小可用卡牌定义。</summary>
    internal static class MatchTestCards
    {
        public static CardDefinition Minion(
            string key = "TEST_MINION",
            int attack = 3,
            int health = 2,
            int cost = 2)
        {
            return new CardDefinition
            {
                Id = attack * 100 + health,
                Key = key,
                Cost = cost,
                Type = CardType.Minion,
                Attack = attack,
                Health = health
            };
        }

        public static CardDefinition Spell(string key = "TEST_SPELL", int cost = 4)
        {
            return new CardDefinition
            {
                Id = 9000 + cost,
                Key = key,
                Cost = cost,
                Type = CardType.Spell,
                TargetRule = TargetRule.None
            };
        }
    }
}
