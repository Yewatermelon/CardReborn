using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M4-T3：控制器事件流——序列与结算顺序一致、拒绝不产事件、疲劳/终局事件（AC-1~5,7）。
    /// </summary>
    [TestFixture]
    public sealed class MatchControllerEventFlowTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        [Test]
        public void EndTurn_EmitsExactSevenEvents_InSettlementOrder()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            controller.Submit(new EndTurnCommand(0));

            IReadOnlyList<GameEvent> events = controller.Events;
            Assert.That(events.Count, Is.EqualTo(7));

            Assert.That(events[0], Is.TypeOf<PhaseChangedEvent>());
            PhaseChangedEvent first = (PhaseChangedEvent)events[0];
            Assert.That(first.From, Is.EqualTo(TurnPhase.Main));
            Assert.That(first.To, Is.EqualTo(TurnPhase.TurnEnd));

            Assert.That(events[1], Is.TypeOf<TurnEndedEvent>());
            Assert.That(events[2], Is.TypeOf<PhaseChangedEvent>());
            Assert.That(events[3], Is.TypeOf<TurnStartedEvent>());
            Assert.That(events[4], Is.TypeOf<PhaseChangedEvent>());

            // 第 5 个：抽牌（牌库空→疲劳；牌库非空→抽牌）。本夹具无牌库，必为疲劳。
            Assert.That(events[5], Is.TypeOf<FatigueEvent>());

            Assert.That(events[6], Is.TypeOf<PhaseChangedEvent>());
            PhaseChangedEvent last = (PhaseChangedEvent)events[6];
            Assert.That(last.From, Is.EqualTo(TurnPhase.Draw));
            Assert.That(last.To, Is.EqualTo(TurnPhase.Main));
        }

        [Test]
        public void Events_SequenceIsMonotonicFromZero()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            controller.Submit(new EndTurnCommand(0));
            controller.Submit(new EndTurnCommand(1));

            for (int i = 0; i < controller.Events.Count; i++)
            {
                Assert.That(controller.Events[i].Sequence, Is.EqualTo(i),
                    "事件 " + i + " 序号不连续");
            }
        }

        [Test]
        public void RejectedCommand_EmitsNoEvents_ButRecordsLedger()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            controller.Submit(new EndTurnCommand(1));

            Assert.That(controller.Events.Count, Is.EqualTo(0));
            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.History[0].Accepted, Is.False);
        }

        [Test]
        public void FatigueEvent_DamageAndCounterMatchDrawService()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            controller.Submit(new EndTurnCommand(0));

            FatigueEvent fatigue = controller.Events.OfType<FatigueEvent>().Single();
            Assert.That(fatigue.Seat, Is.EqualTo(1));
            Assert.That(fatigue.Damage, Is.EqualTo(1));
            Assert.That(fatigue.FatigueCounter, Is.EqualTo(1));
        }

        [Test]
        public void FatigueEvent_IncrementsAcrossTurns()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            controller.Submit(new EndTurnCommand(0)); // 座 1 疲劳 1
            controller.Submit(new EndTurnCommand(1)); // 座 0 疲劳 1

            List<FatigueEvent> fatigues = controller.Events.OfType<FatigueEvent>().ToList();
            Assert.That(fatigues.Count, Is.EqualTo(2));
            Assert.That(fatigues[0].Seat, Is.EqualTo(1));
            Assert.That(fatigues[0].FatigueCounter, Is.EqualTo(1));
            Assert.That(fatigues[1].Seat, Is.EqualTo(0));
            Assert.That(fatigues[1].FatigueCounter, Is.EqualTo(1));
        }

        [Test]
        public void DrawnCardEmitted_WhenDeckHasCard()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            MatchState state = MatchControllerFixtures.NewMatch(
                db, MatchControllerFixtures.LoadEnabledDeckKeys(), seed: 5);
            int first = state.ActivePlayerId;
            MatchController controller = new MatchController(state, db);

            controller.Submit(new EndTurnCommand(first));

            CardDrawnEvent drawn = controller.Events.OfType<CardDrawnEvent>().Single();
            Assert.That(drawn.Seat, Is.EqualTo(1 - first));
            Assert.That(state.GetPlayer(1 - first).Hand.Cards
                .Any(c => c.InstanceId == drawn.CardInstanceId), Is.True);
        }

        [Test]
        public void FullFatigueScript_EndsWithFatalFatigueThenMatchEnded()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            MatchState state = MatchControllerFixtures.NewMatch(
                db, MatchControllerFixtures.LoadEnabledDeckKeys(), seed: 20261006);
            int first = state.ActivePlayerId;
            MatchController controller = new MatchController(state, db);

            MatchControllerFixtures.SubmitUntilFinished(controller, first);

            GameEvent last = controller.Events[controller.Events.Count - 1];

            // 致命疲劳发生在 Draw 阶段，之后结算器仍会发出 PhaseChanged(Draw→Main)，
            // 控制器终局判定后才发 MatchEndedEvent；故 FatigueEvent 不必是倒数第二。
            FatigueEvent fatal = controller.Events.OfType<FatigueEvent>().Last();
            Assert.That(fatal.Damage, Is.EqualTo(8));
            Assert.That(fatal.FatigueCounter, Is.EqualTo(8));

            Assert.That(last, Is.TypeOf<MatchEndedEvent>());
            MatchEndedEvent ended = (MatchEndedEvent)last;
            Assert.That(ended.WinnerId, Is.EqualTo(controller.LastOutcome!.WinnerId));
            Assert.That(ended.Result, Is.EqualTo(controller.LastOutcome.Result));
            Assert.That(ended.TurnNumber, Is.EqualTo(controller.LastOutcome.TurnNumber));
        }

        [Test]
        public void HistoryLedger_IndependentOfEvents()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            controller.Submit(new EndTurnCommand(0));

            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.History[0].Accepted, Is.True);
            Assert.That(controller.Events.Count, Is.GreaterThan(0));
        }
    }
}
