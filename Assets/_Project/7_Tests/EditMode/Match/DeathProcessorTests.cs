using System;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Effects;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T7 AC-1~5：DeathProcessor 死亡收集、亡语链式、多随从、英雄不处理。</summary>
    [TestFixture]
    public sealed class DeathProcessorTests
    {
        private static CardDefinition Minion(string key, int atk, int hp, params string[] effects)
        {
            return new CardDefinition
            {
                Id = Math.Abs(key.GetHashCode()),
                Key = key,
                Cost = 1,
                Type = CardType.Minion,
                Attack = atk,
                Health = hp,
                TargetRule = TargetRule.None,
                Effects = effects
            };
        }

        private static CardDatabase BuildDb(params CardDefinition[] cards)
        {
            HeroDefinition[] heroes =
            {
                new HeroDefinition { Id = 1, Key = "H_A", Health = 30, HeroPowerKey = "P_A" },
                new HeroDefinition { Id = 2, Key = "H_B", Health = 30, HeroPowerKey = "P_B" }
            };
            HeroPowerDefinition[] powers =
            {
                new HeroPowerDefinition { Id = 1, Key = "P_A", Cost = 2, TargetRule = TargetRule.None },
                new HeroPowerDefinition { Id = 2, Key = "P_B", Cost = 2, TargetRule = TargetRule.None }
            };
            return new CardDatabase(new ConfigBundle(
                cards: cards,
                heroes: heroes,
                heroPowers: powers,
                rarityWeights: new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100 } },
                gacha: new GachaConfig(),
                rules: new RulesConfig { BoardLimit = 1000 }));
        }

        private static SettlementContext NewCtx(MatchState state, CardDatabase db)
        {
            return new SettlementContext(
                state, db, new TurnStateMachine(state), new EventLog(), new TriggerDispatcher());
        }

        [Test]
        public void SingleDeadMinion_RemovedFromBoard_ToGraveyard_EmitsEvent()
        {
            CardDatabase db = BuildDb(Minion("M", 2, 5));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance dead = CardInstance.FromDefinition(db.RequireCard("M"), 100, 0);
            dead.Health = 0;
            self.Board.Add(dead);

            SettlementContext ctx = NewCtx(state, db);
            new DeathProcessor().Process(ctx);

            Assert.That(self.Board.Contains(dead), Is.False);
            Assert.That(self.Graveyard.Contains(dead), Is.True);
            Assert.That(ctx.Events.Events.Any(e => e is CardDeathEvent), Is.True);
        }

        [Test]
        public void Deathrattle_ExecutesEffect()
        {
            // A 亡语治疗自己英雄 10；英雄先受伤到 20，亡语后回满到 30
            CardDatabase db = BuildDb(
                Minion("A", 1, 1, "OnDeath:HealEffect:10"));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            self.Hero.Health = 20;
            CardInstance a = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            a.Health = 0;
            self.Board.Add(a);

            SettlementContext ctx = NewCtx(state, db);
            new DeathProcessor().Process(ctx);

            Assert.That(self.Board.Count, Is.EqualTo(0));
            Assert.That(self.Graveyard.Count, Is.EqualTo(1));
            Assert.That(self.Hero.Health, Is.EqualTo(30)); // 亡语治疗生效
        }

        [Test]
        public void MultipleDeadMinions_AllRemoved_AllToGraveyard()
        {
            CardDatabase db = BuildDb(Minion("X", 1, 1), Minion("Y", 1, 1));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance x = CardInstance.FromDefinition(db.RequireCard("X"), 100, 0);
            CardInstance y = CardInstance.FromDefinition(db.RequireCard("Y"), 101, 0);
            x.Health = 0;
            y.Health = 0;
            self.Board.Add(x);
            self.Board.Add(y);

            SettlementContext ctx = NewCtx(state, db);
            new DeathProcessor().Process(ctx);

            Assert.That(self.Board.Count, Is.EqualTo(0));
            Assert.That(self.Graveyard.Count, Is.EqualTo(2));
        }

        [Test]
        public void DeathrattleSummonsMinion_NewMinionStaysOnBoard()
        {
            CardDatabase db = BuildDb(
                Minion("A", 1, 1, "OnDeath:SummonEffect:C/1"),
                Minion("C", 2, 3));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance a = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            a.Health = 0;
            self.Board.Add(a);

            SettlementContext ctx = NewCtx(state, db);
            new DeathProcessor().Process(ctx);

            // A 进坟场，C 被召唤上场
            Assert.That(self.Graveyard.Count, Is.EqualTo(1));
            Assert.That(self.Graveyard.Cards[0].CardKey, Is.EqualTo("A"));
            Assert.That(self.Board.Count, Is.EqualTo(1));
            Assert.That(self.Board.Cards[0].CardKey, Is.EqualTo("C"));
        }

        [Test]
        public void HeroDeath_NotHandledByDeathProcessor()
        {
            CardDatabase db = BuildDb(Minion("M", 1, 5));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState enemy = state.GetPlayer(1);
            enemy.Hero.Health = 0;

            SettlementContext ctx = NewCtx(state, db);
            new DeathProcessor().Process(ctx);

            // 英雄 Health 仍为 0，但未进坟场、未被死亡管线处理（留给 MatchEvaluator）
            Assert.That(enemy.Hero.Health, Is.EqualTo(0));
        }

        [Test]
        public void BothPlayers_DeadMinionsProcessed()
        {
            CardDatabase db = BuildDb(Minion("X", 1, 1), Minion("Y", 1, 1));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState p0 = state.GetPlayer(0);
            PlayerState p1 = state.GetPlayer(1);
            CardInstance x = CardInstance.FromDefinition(db.RequireCard("X"), 100, 0);
            CardInstance y = CardInstance.FromDefinition(db.RequireCard("Y"), 101, 1);
            x.Health = 0;
            y.Health = 0;
            p0.Board.Add(x);
            p1.Board.Add(y);

            SettlementContext ctx = NewCtx(state, db);
            new DeathProcessor().Process(ctx);

            Assert.That(p0.Board.Count, Is.EqualTo(0));
            Assert.That(p1.Board.Count, Is.EqualTo(0));
            Assert.That(p0.Graveyard.Count, Is.EqualTo(1));
            Assert.That(p1.Graveyard.Count, Is.EqualTo(1));
        }
    }
}
