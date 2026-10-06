using System;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Effects;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M4-T5 AC-4~8：TriggerDispatcher 分发与链式不死循环、深度超限、时机过滤。
    /// </summary>
    [TestFixture]
    public sealed class TriggerDispatcherTests
    {
        private static CardDefinition MinionWith(string key, int cost, params string[] effects)
        {
            return new CardDefinition
            {
                Id = Math.Abs(key.GetHashCode()),
                Key = key,
                Cost = cost,
                Type = CardType.Minion,
                Attack = 1,
                Health = 1,
                TargetRule = effects.Any(e => e.Contains("Damage") && !e.Contains("OnDeath"))
                    ? TargetRule.Any
                    : TargetRule.None,
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

        private static SettlementContext NewCtx(MatchState state, CardDatabase db, TriggerDispatcher dispatcher)
        {
            return new SettlementContext(
                state, db, new TurnStateMachine(state), new EventLog(), dispatcher);
        }

        [Test]
        public void RaiseOnDeath_ChainSummon_NoInfiniteLoop()
        {
            CardDatabase db = BuildDb(
                MinionWith("A", 2, "OnDeath:SummonEffect:B,1"),
                MinionWith("B", 2, "OnDeath:SummonEffect:C,1"),
                MinionWith("C", 2));

            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance a = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            self.Board.Add(a);

            TriggerDispatcher dispatcher = new TriggerDispatcher();
            SettlementContext ctx = NewCtx(state, db, dispatcher);
            // 模拟 A 死亡：从场上移除后触发亡语
            self.Board.Remove(a);
            dispatcher.RaiseOnDeath(ctx, a);

            // A 亡语召唤 B 上场；B 未死亡不触发其亡语；场上应有 B。
            Assert.That(self.Board.Count, Is.EqualTo(1));
            Assert.That(self.Board.Cards[0].CardKey, Is.EqualTo("B"));
        }

        [Test]
        public void RaiseOnSummon_DeepChain_ExceedsDepth_Throws()
        {
            // X 的 OnSummon 召唤 X 自己（无限递归）；战场容量放大以先触发深度限制。
            CardDatabase db = BuildDb(MinionWith("X", 2, "OnSummon:SummonEffect:X,1"));
            MatchState state = RuleEngineTestHelpers.BuildState(
                activePlayerId: 0, rules: new RulesConfig { BoardLimit = 1000 });
            PlayerState self = state.GetPlayer(0);
            CardInstance x = CardInstance.FromDefinition(db.RequireCard("X"), 200, 0);
            self.Board.Add(x);

            TriggerDispatcher dispatcher = new TriggerDispatcher();
            SettlementContext ctx = NewCtx(state, db, dispatcher);

            Assert.That(() => dispatcher.RaiseOnSummon(ctx, x),
                Throws.InstanceOf<InvalidOperationException>()
                    .With.Message.Contains("深度"));
        }

        [Test]
        public void RaiseOnPlay_OnlyTriggersOnPlay_NotOnDeath()
        {
            CardDatabase db = BuildDb(
                MinionWith("BC", 2, "OnPlay:DamageEffect:3", "OnDeath:HealEffect:3"));

            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance card = CardInstance.FromDefinition(db.RequireCard("BC"), 300, 0);
            self.Hand.Add(card);

            TriggerDispatcher dispatcher = new TriggerDispatcher();
            SettlementContext ctx = NewCtx(state, db, dispatcher);
            dispatcher.RaiseOnPlay(ctx, card, TargetRef.ForHero(1));

            Assert.That(enemy.Hero.Health, Is.EqualTo(27));
            Assert.That(self.Hero.Health, Is.EqualTo(30));
        }

        [Test]
        public void RaiseOnTurnStart_TriggersBoardMinionEffects()
        {
            CardDatabase db = BuildDb(MinionWith("TS", 2, "OnTurnStart:DrawCardEffect:1"));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance deckCard = CardInstance.FromDefinition(
                MinionWith("DRAW", 1), 500, 0);
            self.Deck.Add(deckCard);
            CardInstance boardMinion = CardInstance.FromDefinition(db.RequireCard("TS"), 301, 0);
            self.Board.Add(boardMinion);

            TriggerDispatcher dispatcher = new TriggerDispatcher();
            SettlementContext ctx = NewCtx(state, db, dispatcher);
            dispatcher.RaiseOnTurnStart(ctx, 0);

            Assert.That(self.Hand.Contains(deckCard), Is.True);
        }

        [Test]
        public void RaiseOnTurnEnd_TriggersBoardMinionEffects()
        {
            CardDatabase db = BuildDb(MinionWith("TE", 2, "OnTurnEnd:DamageEffect:1"));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance boardMinion = CardInstance.FromDefinition(db.RequireCard("TE"), 302, 0);
            self.Board.Add(boardMinion);

            TriggerDispatcher dispatcher = new TriggerDispatcher();
            SettlementContext ctx = NewCtx(state, db, dispatcher);
            dispatcher.RaiseOnTurnEnd(ctx, 0);

            // 目标 None 时伤害对自己英雄生效（T4 语义）。
            Assert.That(self.Hero.Health, Is.EqualTo(29));
        }
    }
}
