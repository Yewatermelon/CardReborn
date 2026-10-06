using System.Linq;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T6 AC-1/2/3/4/5/6/7：AttackSettler 端到端攻击结算。</summary>
    [TestFixture]
    public sealed class AttackSettlerTests
    {
        private static CardDefinition Minion(
            string key, int atk, int hp, Keyword keywords = Keyword.None)
        {
            return new CardDefinition
            {
                Id = System.Math.Abs(key.GetHashCode()),
                Key = key,
                Cost = 1,
                Type = CardType.Minion,
                Attack = atk,
                Health = hp,
                TargetRule = TargetRule.None,
                Keywords = keywords
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
                rules: new RulesConfig()));
        }

        private static (MatchState state, CardDatabase db, SettlementContext ctx) NewGame(
            params CardDefinition[] cards)
        {
            CardDatabase db = BuildDb(cards);
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            SettlementContext ctx = new SettlementContext(
                state, db, new TurnStateMachine(state), new EventLog(),
                new Card.Application.Match.Effects.TriggerDispatcher());
            return (state, db, ctx);
        }

        [Test]
        public void AttackHero_DealsDamageToHero_AttackerUnharmed()
        {
            var (state, db, ctx) = NewGame(Minion("ATK", 4, 3));
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("ATK"), 100, 0);
            self.Board.Add(attacker);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForHero(1)));

            Assert.That(enemy.Hero.Health, Is.EqualTo(26));
            Assert.That(attacker.Health, Is.EqualTo(3));
            Assert.That(attacker.AttacksUsedThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void AttackMinion_BothTakeDamageSimultaneously()
        {
            var (state, db, ctx) = NewGame(Minion("A", 3, 5), Minion("D", 2, 7));
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            CardInstance defender = CardInstance.FromDefinition(db.RequireCard("D"), 200, 1);
            self.Board.Add(attacker);
            enemy.Board.Add(defender);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForMinion(200)));

            // 攻击者(3/5) 受防御者攻击 2 → 3 血；防御者(2/7) 受攻击者攻击 3 → 4 血
            Assert.That(attacker.Health, Is.EqualTo(3));
            Assert.That(defender.Health, Is.EqualTo(4));
        }

        [Test]
        public void AttackDivineShieldMinion_ShieldConsumes_NoHealthLoss()
        {
            var (state, db, ctx) = NewGame(
                Minion("A", 3, 5),
                Minion("DS", 1, 4, Keyword.DivineShield));
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            CardInstance defender = CardInstance.FromDefinition(db.RequireCard("DS"), 200, 1);
            self.Board.Add(attacker);
            enemy.Board.Add(defender);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForMinion(200)));

            Assert.That(defender.Health, Is.EqualTo(4));
            Assert.That(defender.Statuses.Has(StatusFlags.DivineShield), Is.False);
            Assert.That(attacker.Health, Is.EqualTo(4)); // 攻击者受防御者 1 点伤害
        }

        [Test]
        public void PoisonousAttacker_DestroysDefenderMinion()
        {
            var (state, db, ctx) = NewGame(
                Minion("P", 1, 2, Keyword.Poisonous),
                Minion("D", 5, 10));
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("P"), 100, 0);
            CardInstance defender = CardInstance.FromDefinition(db.RequireCard("D"), 200, 1);
            self.Board.Add(attacker);
            enemy.Board.Add(defender);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForMinion(200)));

            Assert.That(defender.Health, Is.EqualTo(0));
        }

        [Test]
        public void ZeroAttack_ConsumesAttackCount_NoDamage()
        {
            var (state, db, ctx) = NewGame(Minion("Z", 0, 3), Minion("D", 0, 3));
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("Z"), 100, 0);
            CardInstance defender = CardInstance.FromDefinition(db.RequireCard("D"), 200, 1);
            self.Board.Add(attacker);
            enemy.Board.Add(defender);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForMinion(200)));

            Assert.That(attacker.AttacksUsedThisTurn, Is.EqualTo(1));
            Assert.That(attacker.Health, Is.EqualTo(3));
            Assert.That(defender.Health, Is.EqualTo(3));
        }

        [Test]
        public void DeadMinion_StaysOnBoard_DeathHandledByT7()
        {
            var (state, db, ctx) = NewGame(Minion("A", 10, 1), Minion("D", 10, 1));
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            CardInstance defender = CardInstance.FromDefinition(db.RequireCard("D"), 200, 1);
            self.Board.Add(attacker);
            enemy.Board.Add(defender);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForMinion(200)));

            Assert.That(attacker.Health, Is.LessThanOrEqualTo(0));
            Assert.That(defender.Health, Is.LessThanOrEqualTo(0));
            // 仍在场上（T7 负责移除）
            Assert.That(self.Board.Contains(attacker), Is.True);
            Assert.That(enemy.Board.Contains(defender), Is.True);
        }

        [Test]
        public void AttackEmitsEvents()
        {
            var (state, db, ctx) = NewGame(Minion("A", 2, 3));
            PlayerState self = state.GetPlayer(0);
            CardInstance attacker = CardInstance.FromDefinition(db.RequireCard("A"), 100, 0);
            self.Board.Add(attacker);

            new AttackSettler().Settle(ctx, new AttackCommand(0, 100, TargetRef.ForHero(1)));

            System.Collections.Generic.IReadOnlyList<GameEvent> events = ctx.Events.Events;
            Assert.That(events.Any(e => e is AttackDeclaredEvent), Is.True);
            Assert.That(events.Any(e => e is DamageEvent), Is.True);
        }
    }
}
