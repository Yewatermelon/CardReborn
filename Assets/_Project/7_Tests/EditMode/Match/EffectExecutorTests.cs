using System.Linq;
using Card.Application.Match.Effects;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M4-T4 AC-2~7：5 个效果执行器各自正确（独立测试，不走出牌链路）。
    /// </summary>
    [TestFixture]
    public sealed class EffectExecutorTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        private static EffectContext NewContext(
            MatchState state, CardInstance source, int selfSeat, TargetRef target)
        {
            return new EffectContext(state, Db, new EventLog(), source, selfSeat, target);
        }

        [Test]
        public void Damage_Hero_ArmorFirst_ThenHealth()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState enemy = state.GetPlayer(1);
            enemy.Hero.Armor = 3;
            CardInstance src = RuleEngineTestHelpers.AddToHand(
                state.GetPlayer(0), MatchTestCards.Spell("D"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.ForHero(1));
            new EffectExecutor().Execute(new DamageEffectData(5), ctx);

            Assert.That(enemy.Hero.Armor, Is.EqualTo(0));
            Assert.That(enemy.Hero.Health, Is.EqualTo(28));
            DamageEvent e = ctx.Events.Events.OfType<DamageEvent>().Single();
            Assert.That(e.TargetHeroSeat, Is.EqualTo(1));
            Assert.That(e.Amount, Is.EqualTo(5));
            Assert.That(e.DivineShieldConsumed, Is.False);
        }

        [Test]
        public void Damage_Minion_ReducesHealth()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance target = RuleEngineTestHelpers.AddToBoard(enemy, MatchTestCards.Minion("T", 2, 4), 50);
            CardInstance src = RuleEngineTestHelpers.AddToHand(
                state.GetPlayer(0), MatchTestCards.Spell("D"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.ForMinion(50));
            new EffectExecutor().Execute(new DamageEffectData(2), ctx);

            Assert.That(target.Health, Is.EqualTo(2));
            DamageEvent e = ctx.Events.Events.OfType<DamageEvent>().Single();
            Assert.That(e.TargetInstanceId, Is.EqualTo(50));
        }

        [Test]
        public void Heal_Hero_CappedAtMax()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            self.Hero.Health = 20;
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("H"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.ForHero(0));
            new EffectExecutor().Execute(new HealEffectData(15), ctx);

            Assert.That(self.Hero.Health, Is.EqualTo(30));
            HealingEvent e = ctx.Events.Events.OfType<HealingEvent>().Single();
            Assert.That(e.TargetHeroSeat, Is.EqualTo(0));
        }

        [Test]
        public void Heal_Minion_CappedAtMaxHealth()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance target = RuleEngineTestHelpers.AddToBoard(self, MatchTestCards.Minion("T", 2, 4), 50);
            target.Health = 1;
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("H"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.ForMinion(50));
            new EffectExecutor().Execute(new HealEffectData(10), ctx);

            Assert.That(target.Health, Is.EqualTo(4));
        }

        [Test]
        public void DrawCard_AddsToHand()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance deckCard = CardInstance.FromDefinition(
                MatchTestCards.Minion("DRAW_ME", 1, 1, 1), 200, 0);
            self.Deck.Add(deckCard);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("DRAW"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            new EffectExecutor().Execute(new DrawCardEffectData(1), ctx);

            Assert.That(self.Hand.Contains(deckCard), Is.True);
            Assert.That(self.Deck.Contains(deckCard), Is.False);
            Assert.That(ctx.Events.Events.OfType<CardDrawnEvent>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void DrawCard_EmptyDeck_Fatigue()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("DRAW"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            new EffectExecutor().Execute(new DrawCardEffectData(1), ctx);

            Assert.That(self.FatigueCounter, Is.EqualTo(1));
            Assert.That(self.Hero.Health, Is.EqualTo(29));
            Assert.That(ctx.Events.Events.OfType<FatigueEvent>().Single().Damage, Is.EqualTo(1));
        }

        [Test]
        public void Summon_AddsMinionToOwnBoard()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("SUMMON"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            new EffectExecutor().Execute(new SummonEffectData("M1", 1), ctx);

            Assert.That(self.Board.Count, Is.EqualTo(1));
            CardInstance summoned = self.Board.Cards[0];
            Assert.That(summoned.CardKey, Is.EqualTo("M1"));
            Assert.That(summoned.InstanceId, Is.Not.EqualTo(src.InstanceId));
        }

        [Test]
        public void Buff_IncreasesAttackAndHealth()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance target = RuleEngineTestHelpers.AddToBoard(self, MatchTestCards.Minion("T", 2, 3), 50);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("BUFF"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.ForMinion(50));
            new EffectExecutor().Execute(new BuffEffectData(2, 3), ctx);

            Assert.That(target.Attack, Is.EqualTo(4));
            Assert.That(target.Health, Is.EqualTo(6));
        }

        [Test]
        public void Damage_NoTarget_AppliesToOwnHero()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("D"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            new EffectExecutor().Execute(new DamageEffectData(3), ctx);

            Assert.That(self.Hero.Health, Is.EqualTo(27));
        }

        [Test]
        public void GainMana_AddsCurrentMana()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            self.Mana.Restore(0, 0);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("GM"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            new EffectExecutor().Execute(new GainManaEffectData(2), ctx);

            Assert.That(self.Mana.Current, Is.EqualTo(2));
        }

        [Test]
        public void GainArmor_AddsArmorToSelfHero()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("GA"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            new EffectExecutor().Execute(new GainArmorEffectData(4), ctx);

            Assert.That(self.Hero.Armor, Is.EqualTo(4));
        }

        [Test]
        public void Destroy_RemovesTargetMinion_EmitsDeathEvent()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance target = RuleEngineTestHelpers.AddToBoard(enemy, MatchTestCards.Minion("T", 2, 3), 50);
            CardInstance src = RuleEngineTestHelpers.AddToHand(
                state.GetPlayer(0), MatchTestCards.Spell("DEST"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.ForMinion(50));
            new EffectExecutor().Execute(new DestroyEffectData(), ctx);

            Assert.That(enemy.Board.Contains(target), Is.False);
            Assert.That(enemy.Graveyard.Contains(target), Is.True);
            Assert.That(ctx.Events.Events.OfType<CardDeathEvent>().Single().CardInstanceId, Is.EqualTo(50));
        }

        [Test]
        public void Composite_ExecutesAllSubEffects()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            self.Mana.Restore(0, 0);
            CardInstance src = RuleEngineTestHelpers.AddToHand(self, MatchTestCards.Spell("CMP"), 100);

            EffectContext ctx = NewContext(state, src, 0, TargetRef.None);
            var composite = new CompositeEffectData(new IEffectData[]
            {
                new GainArmorEffectData(3),
                new GainManaEffectData(1)
            });
            new EffectExecutor().Execute(composite, ctx);

            Assert.That(self.Hero.Armor, Is.EqualTo(3));
            Assert.That(self.Mana.Current, Is.EqualTo(1));
        }
    }
}
