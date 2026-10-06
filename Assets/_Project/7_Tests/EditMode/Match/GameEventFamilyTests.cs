using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T3：GameEvent 家族 12 个类型的构造与字段断言（AC-6）。</summary>
    [TestFixture]
    public sealed class GameEventFamilyTests
    {
        [Test]
        public void PhaseChangedEvent_AssignsFields()
        {
            PhaseChangedEvent e = new PhaseChangedEvent(TurnPhase.Main, TurnPhase.TurnEnd);
            Assert.That(e.From, Is.EqualTo(TurnPhase.Main));
            Assert.That(e.To, Is.EqualTo(TurnPhase.TurnEnd));
        }

        [Test]
        public void TurnStartedEvent_AssignsFields()
        {
            TurnStartedEvent e = new TurnStartedEvent(turnNumber: 3, activeSeat: 1);
            Assert.That(e.TurnNumber, Is.EqualTo(3));
            Assert.That(e.ActiveSeat, Is.EqualTo(1));
        }

        [Test]
        public void TurnEndedEvent_AssignsFields()
        {
            TurnEndedEvent e = new TurnEndedEvent(turnNumber: 2, activeSeat: 0);
            Assert.That(e.TurnNumber, Is.EqualTo(2));
            Assert.That(e.ActiveSeat, Is.EqualTo(0));
        }

        [Test]
        public void CardDrawnAndBurnedEvents_AssignFields()
        {
            CardDrawnEvent drawn = new CardDrawnEvent(seat: 0, cardInstanceId: 42);
            Assert.That(drawn.Seat, Is.EqualTo(0));
            Assert.That(drawn.CardInstanceId, Is.EqualTo(42));

            CardBurnedEvent burned = new CardBurnedEvent(seat: 1, cardInstanceId: 7);
            Assert.That(burned.Seat, Is.EqualTo(1));
            Assert.That(burned.CardInstanceId, Is.EqualTo(7));
        }

        [Test]
        public void FatigueEvent_AssignsFields()
        {
            FatigueEvent e = new FatigueEvent(seat: 0, damage: 3, fatigueCounter: 3);
            Assert.That(e.Seat, Is.EqualTo(0));
            Assert.That(e.Damage, Is.EqualTo(3));
            Assert.That(e.FatigueCounter, Is.EqualTo(3));
        }

        [Test]
        public void DamageEvent_AssignsFields()
        {
            DamageEvent e = new DamageEvent(
                sourceInstanceId: 10, targetInstanceId: 20, targetHeroSeat: null, amount: 4,
                divineShieldConsumed: false);
            Assert.That(e.SourceInstanceId, Is.EqualTo(10));
            Assert.That(e.TargetInstanceId, Is.EqualTo(20));
            Assert.That(e.TargetHeroSeat, Is.Null);
            Assert.That(e.Amount, Is.EqualTo(4));
            Assert.That(e.DivineShieldConsumed, Is.False);
        }

        [Test]
        public void HealingEvent_AssignsFields()
        {
            HealingEvent e = new HealingEvent(targetInstanceId: null, targetHeroSeat: 1, amount: 5);
            Assert.That(e.TargetInstanceId, Is.Null);
            Assert.That(e.TargetHeroSeat, Is.EqualTo(1));
            Assert.That(e.Amount, Is.EqualTo(5));
        }

        [Test]
        public void CardDeathEvent_AssignsFields()
        {
            CardDeathEvent e = new CardDeathEvent(cardInstanceId: 99);
            Assert.That(e.CardInstanceId, Is.EqualTo(99));
        }

        [Test]
        public void CardPlayedEvent_AssignsFields()
        {
            CardPlayedEvent e = new CardPlayedEvent(seat: 0, cardInstanceId: 5);
            Assert.That(e.Seat, Is.EqualTo(0));
            Assert.That(e.CardInstanceId, Is.EqualTo(5));
        }

        [Test]
        public void AttackDeclaredEvent_AssignsFields()
        {
            AttackDeclaredEvent e = new AttackDeclaredEvent(
                attackerInstanceId: 11, targetInstanceId: null, targetHeroSeat: 1);
            Assert.That(e.AttackerInstanceId, Is.EqualTo(11));
            Assert.That(e.TargetInstanceId, Is.Null);
            Assert.That(e.TargetHeroSeat, Is.EqualTo(1));
        }

        [Test]
        public void MatchEndedEvent_AssignsFields()
        {
            MatchEndedEvent e = new MatchEndedEvent(
                MatchResult.Player0Wins, winnerId: 0, turnNumber: 68, reason: "英雄归零");
            Assert.That(e.Result, Is.EqualTo(MatchResult.Player0Wins));
            Assert.That(e.WinnerId, Is.EqualTo(0));
            Assert.That(e.TurnNumber, Is.EqualTo(68));
            Assert.That(e.Reason, Is.EqualTo("英雄归零"));
        }

        [Test]
        public void EventLog_AssignsMonotonicSequence()
        {
            EventLog log = new EventLog();
            log.Emit(new TurnStartedEvent(1, 0));
            log.Emit(new PhaseChangedEvent(TurnPhase.TurnStart, TurnPhase.Draw));

            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log.Events[0].Sequence, Is.EqualTo(0));
            Assert.That(log.Events[1].Sequence, Is.EqualTo(1));
            Assert.That(log.Last, Is.SameAs(log.Events[1]));
        }
    }
}
