using System;
using System.Linq;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T8 AC-1~5：英雄技能消耗法力、执行效果、本回合只能用一次、目标随从、无目标技能。</summary>
    [TestFixture]
    public sealed class HeroPowerSettlerTests
    {
        private static CardDatabase BuildDb(params CardDefinition[] cards)
        {
            // 必须与 RuleEngineTestHelpers.BuildState() 的英雄技能 Key 一致（"POWER_A"/"POWER_B"）
            HeroDefinition[] heroes =
            {
                new HeroDefinition { Id = 1, Key = "HERO_A", Health = 30, HeroPowerKey = "POWER_A" },
                new HeroDefinition { Id = 2, Key = "HERO_B", Health = 30, HeroPowerKey = "POWER_B" }
            };
            HeroPowerDefinition[] powers =
            {
                new HeroPowerDefinition { Id = 1, Key = "POWER_A", Cost = 2, TargetRule = TargetRule.Any, Effects = new[] { "DamageEffect:1" } },
                new HeroPowerDefinition { Id = 2, Key = "POWER_B", Cost = 2, TargetRule = TargetRule.Any, Effects = new[] { "DamageEffect:1" } }
            };
            return new CardDatabase(new ConfigBundle(
                cards: cards,
                heroes: heroes,
                heroPowers: powers,
                rarityWeights: new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100 } },
                gacha: new GachaConfig(),
                rules: new RulesConfig { BoardLimit = 1000 }));
        }

        private static MatchState BuildState(int activePlayerId = 0, TurnPhase phase = TurnPhase.Main)
        {
            return RuleEngineTestHelpers.BuildState(activePlayerId, phase);
        }

        [Test]
        public void HeroPower_ConsumesMana()
        {
            CardDatabase db = BuildDb();
            MatchState state = BuildState();
            PlayerState player = state.GetPlayer(0);
            player.Mana.Spend(10 - 3); // 留 3 法力

            MatchController controller = new MatchController(state, db);
            CommandResult result = controller.Submit(new UseHeroPowerCommand(0, TargetRef.ForHero(1)));

            Assert.That(result.IsValid, Is.True);
            Assert.That(player.Mana.Current, Is.EqualTo(1)); // 3 - 2 = 1
        }

        [Test]
        public void HeroPower_ExecutesDamageEffect_OnEnemyHero()
        {
            CardDatabase db = BuildDb();
            MatchState state = BuildState();
            PlayerState enemy = state.GetPlayer(1);
            int before = enemy.Hero.Health;

            MatchController controller = new MatchController(state, db);
            CommandResult result = controller.Submit(new UseHeroPowerCommand(0, TargetRef.ForHero(1)));

            Assert.That(result.IsValid, Is.True);
            Assert.That(enemy.Hero.Health, Is.EqualTo(before - 1));
        }

        [Test]
        public void HeroPower_UsedTwiceInSameTurn_RejectedByRuleEngine()
        {
            CardDatabase db = BuildDb();
            MatchState state = BuildState();

            MatchController controller = new MatchController(state, db);
            CommandResult first = controller.Submit(new UseHeroPowerCommand(0, TargetRef.ForHero(1)));
            CommandResult second = controller.Submit(new UseHeroPowerCommand(0, TargetRef.ForHero(1)));

            Assert.That(first.IsValid, Is.True);
            Assert.That(second.Error, Is.EqualTo(CommandError.HeroPowerAlreadyUsed));
            Assert.That(state.GetPlayer(0).Hero.PowerUsedThisTurn, Is.True);
        }

        [Test]
        public void HeroPower_TargetMinion_TakesDamage()
        {
            CardDatabase db = BuildDb(
                new CardDefinition
                {
                    Id = 1, Key = "M", Cost = 1, Type = CardType.Minion, Attack = 2, Health = 5,
                    TargetRule = TargetRule.None, Effects = Array.Empty<string>()
                });
            MatchState state = BuildState();
            PlayerState enemy = state.GetPlayer(1);
            CardInstance minion = CardInstance.FromDefinition(db.RequireCard("M"), 100, 1);
            enemy.Board.Add(minion);

            MatchController controller = new MatchController(state, db);
            CommandResult result = controller.Submit(new UseHeroPowerCommand(0, TargetRef.ForMinion(100)));

            Assert.That(result.IsValid, Is.True);
            Assert.That(minion.Health, Is.EqualTo(4)); // 5 - 1
        }

        [Test]
        public void HeroPower_TargetRuleNone_NoTarget_DamagesSelfHero()
        {
            // TargetRule.None 的技能：无需目标，效果对自己英雄生效（T4 简化）
            HeroDefinition[] heroes =
            {
                new HeroDefinition { Id = 1, Key = "HERO_A", Health = 30, HeroPowerKey = "P_NONE" },
                new HeroDefinition { Id = 2, Key = "HERO_B", Health = 30, HeroPowerKey = "P_NONE" }
            };
            HeroPowerDefinition[] powers =
            {
                new HeroPowerDefinition { Id = 1, Key = "P_NONE", Cost = 2, TargetRule = TargetRule.None, Effects = new[] { "DamageEffect:1" } }
            };
            CardDatabase db = new CardDatabase(new ConfigBundle(
                cards: Array.Empty<CardDefinition>(),
                heroes: heroes,
                heroPowers: powers,
                rarityWeights: new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100 } },
                gacha: new GachaConfig(),
                rules: new RulesConfig { BoardLimit = 1000 }));

            // 手动构造状态，英雄技能 Key 指向 "P_NONE"
            PlayerState p0 = new PlayerState(0, new HeroState("HERO_A", "P_NONE", 30), new ManaPool(10, 10), null);
            PlayerState p1 = new PlayerState(1, new HeroState("HERO_B", "P_NONE", 30), new ManaPool(10, 10), null);
            MatchState state = new MatchState(p0, p1, 0) { Phase = TurnPhase.Main, TurnNumber = 1 };
            PlayerState self = state.GetPlayer(0);
            int before = self.Hero.Health;

            MatchController controller = new MatchController(state, db);
            CommandResult result = controller.Submit(new UseHeroPowerCommand(0, TargetRef.None));

            Assert.That(result.IsValid, Is.True);
            Assert.That(self.Hero.Health, Is.EqualTo(before - 1));
        }

        [Test]
        public void HeroPower_EmitsDamageEvent()
        {
            CardDatabase db = BuildDb();
            MatchState state = BuildState();

            MatchController controller = new MatchController(state, db);
            controller.Submit(new UseHeroPowerCommand(0, TargetRef.ForHero(1)));

            Assert.That(controller.Events.Any(e => e is DamageEvent), Is.True);
        }
    }
}
