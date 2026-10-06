using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T6 测试共享辅助。</summary>
    internal static class RuleEngineTestHelpers
    {
        public static CardDatabase BuildDatabase()
        {
            return new CardDatabase(new ConfigBundle(
                cards: new[]
                {
                    MatchTestCards.Minion("M1", attack: 3, health: 2, cost: 2),
                    MatchTestCards.Minion("M2_TAUNT", attack: 2, health: 3, cost: 2, keywords: Keyword.Taunt),
                    MatchTestCards.Minion("M3_CHARGE", attack: 2, health: 1, cost: 3, keywords: Keyword.Charge),
                    MatchTestCards.Minion("M4_STEALTH", attack: 1, health: 1, cost: 1, keywords: Keyword.Stealth),
                    MatchTestCards.Spell("S1_NO_TARGET", cost: 1),
                    new CardDefinition { Id = 9100, Key = "S2_TARGET_ANY", Cost = 2, Type = CardType.Spell, TargetRule = TargetRule.Any },
                    new CardDefinition { Id = 9101, Key = "S3_TARGET_ENEMY", Cost = 3, Type = CardType.Spell, TargetRule = TargetRule.Enemy },
                    new CardDefinition { Id = 9102, Key = "S4_TARGET_ENEMY_MINION", Cost = 2, Type = CardType.Spell, TargetRule = TargetRule.EnemyMinion }
                },
                heroes: new[]
                {
                    new HeroDefinition { Id = 1, Key = "HERO_A", Health = 30, HeroPowerKey = "POWER_A" },
                    new HeroDefinition { Id = 2, Key = "HERO_B", Health = 30, HeroPowerKey = "POWER_B" }
                },
                heroPowers: new[]
                {
                    new HeroPowerDefinition { Id = 1, Key = "POWER_A", Cost = 2, TargetRule = TargetRule.Any },
                    new HeroPowerDefinition { Id = 2, Key = "POWER_B", Cost = 2, TargetRule = TargetRule.None }
                },
                rarityWeights: new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100, MinPerPack = 0 } },
                gacha: new GachaConfig { PackSize = 5, CoinCost = 100, PityCount = 10, PityRarity = CardRarity.Legendary },
                rules: new RulesConfig()));
        }

        public static MatchState BuildState(int activePlayerId = 0, TurnPhase phase = TurnPhase.Main)
        {
            PlayerState p0 = new PlayerState(0, new HeroState("HERO_A", "POWER_A", 30), new ManaPool(max: 10, current: 10));
            PlayerState p1 = new PlayerState(1, new HeroState("HERO_B", "POWER_B", 30), new ManaPool(max: 10, current: 10));
            return new MatchState(p0, p1, activePlayerId) { Phase = phase, TurnNumber = 1 };
        }

        public static CardInstance AddToHand(PlayerState player, CardDefinition def, int instanceId)
        {
            CardInstance card = CardInstance.FromDefinition(def, instanceId, player.Id);
            player.Hand.Add(card);
            return card;
        }

        public static CardInstance AddToBoard(PlayerState player, CardDefinition def, int instanceId, bool summoningSick = true)
        {
            CardInstance card = CardInstance.FromDefinition(def, instanceId, player.Id);
            player.Board.Add(card);
            if (summoningSick) card.Statuses.Add(StatusFlags.SummoningSickness);
            return card;
        }
    }
}
