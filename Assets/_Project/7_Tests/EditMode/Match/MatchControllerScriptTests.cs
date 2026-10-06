using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M4-T2 AC-9/10/11/12/13：完整"只结束回合"疲劳致死脚本回放（真实源表、同种子可复现）。
    /// </summary>
    [TestFixture]
    public sealed class MatchControllerScriptTests
    {
        private const int Seed = 20261006;
        private const int ExpectedFinishStep = 67;
        private const int QueuedScriptCount = 80;

        private CardDatabase _fullDb = null!;
        private IReadOnlyList<string> _deck = null!;

        [SetUp]
        public void Setup()
        {
            _fullDb = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        [Test]
        public void FullScript_FatigueKillsSecondPlayer_FirstPlayerWins()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            int first = match.ActivePlayerId;
            int second = 1 - first;
            MatchController controller = new MatchController(match, _fullDb);

            int steps = MatchControllerFixtures.SubmitUntilFinished(controller, first);

            Assert.That(steps, Is.EqualTo(ExpectedFinishStep));
            Assert.That(controller.IsFinished, Is.True);
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.MatchEnd));
            Assert.That(controller.LastOutcome, Is.Not.Null);
            Assert.That(controller.LastOutcome!.Result,
                Is.EqualTo(first == 0 ? MatchResult.Player0Wins : MatchResult.Player1Wins));
            Assert.That(controller.LastOutcome.WinnerId, Is.EqualTo(first));
            Assert.That(controller.LastOutcome.TurnNumber, Is.EqualTo(68));

            Assert.That(match.GetPlayer(second).FatigueCounter, Is.EqualTo(8));
            Assert.That(match.GetPlayer(second).Hero.Health, Is.EqualTo(-6));
            Assert.That(match.GetPlayer(first).FatigueCounter, Is.EqualTo(6));
            Assert.That(match.GetPlayer(first).Hero.Health, Is.EqualTo(9));
        }

        [Test]
        public void FullScript_RecordsAcceptedFlow_WithSingleFinishingRecord()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            int first = match.ActivePlayerId;
            MatchController controller = new MatchController(match, _fullDb);

            MatchControllerFixtures.SubmitUntilFinished(controller, first);

            Assert.That(controller.History.Count, Is.EqualTo(ExpectedFinishStep));
            for (int i = 0; i < ExpectedFinishStep - 1; i++)
            {
                Assert.That(controller.History[i].Accepted, Is.True, "第 " + i + " 条应接受");
                Assert.That(controller.History[i].MatchFinished, Is.False);
                Assert.That(controller.History[i].PhaseAfter, Is.EqualTo(TurnPhase.Main));
                Assert.That(controller.History[i].Sequence, Is.EqualTo(i));
            }

            MatchStepRecord last = controller.History[ExpectedFinishStep - 1];
            Assert.That(last.Accepted, Is.True);
            Assert.That(last.MatchFinished, Is.True);
            Assert.That(last.PhaseAfter, Is.EqualTo(TurnPhase.MatchEnd));
        }

        [Test]
        public void FullScript_ProcessPending_StopsAtFinishAndLeavesRestQueued()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            int first = match.ActivePlayerId;
            MatchController controller = new MatchController(match, _fullDb);
            foreach (EndTurnCommand command in
                MatchControllerFixtures.BuildEndTurnScript(first, QueuedScriptCount))
            {
                controller.Enqueue(command);
            }

            int processed = controller.ProcessPending();

            Assert.That(processed, Is.EqualTo(ExpectedFinishStep));
            Assert.That(controller.PendingCount,
                Is.EqualTo(QueuedScriptCount - ExpectedFinishStep));
            Assert.That(controller.IsFinished, Is.True);
        }

        [Test]
        public void FullScript_SameSeed_ReplayProducesIdenticalStateAndLedger()
        {
            MatchState matchA = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            MatchState matchB = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            MatchController controllerA = new MatchController(matchA, _fullDb);
            MatchController controllerB = new MatchController(matchB, _fullDb);

            MatchControllerFixtures.SubmitUntilFinished(controllerA, matchA.ActivePlayerId);
            MatchControllerFixtures.SubmitUntilFinished(controllerB, matchB.ActivePlayerId);

            MatchStateComparer.AssertEqual(matchA, matchB);
            Assert.That(Project(controllerB.History), Is.EqualTo(Project(controllerA.History)));
        }

        [Test]
        public void FullScript_BurnsCardsWhenHandFull_HandNeverExceedsLimit()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            int first = match.ActivePlayerId;
            int second = 1 - first;
            MatchController controller = new MatchController(match, _fullDb);

            for (int i = 0; i < 11; i++)
            {
                controller.Submit(new EndTurnCommand(match.ActivePlayerId));
            }

            // 回合 12 = 后手第 6 次抽牌：手牌 10 张已满，本张爆牌入坟。
            Assert.That(match.ActivePlayerId, Is.EqualTo(second));
            Assert.That(match.GetPlayer(second).Hand.Count, Is.EqualTo(10));
            Assert.That(match.GetPlayer(second).Graveyard.Count, Is.EqualTo(1));

            MatchControllerFixtures.SubmitUntilFinished(controller, match.ActivePlayerId);
            Assert.That(match.GetPlayer(first).Hand.Count, Is.LessThanOrEqualTo(10));
            Assert.That(match.GetPlayer(second).Hand.Count, Is.LessThanOrEqualTo(10));
            Assert.That(match.GetPlayer(first).Graveyard.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Submit_AfterScriptedFinish_RejectedAndStaysFinished()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, Seed);
            int first = match.ActivePlayerId;
            MatchController controller = new MatchController(match, _fullDb);
            MatchControllerFixtures.SubmitUntilFinished(controller, first);

            CommandResult result = controller.Submit(new EndTurnCommand(controller.State.ActivePlayerId));

            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandError.InvalidTarget));
            Assert.That(controller.History.Count, Is.EqualTo(ExpectedFinishStep + 1));
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.MatchEnd));
        }

        private static List<(bool Accepted, CommandError Error, string Type, TurnPhase Phase, bool Finished)>
            Project(IReadOnlyList<MatchStepRecord> records)
        {
            return records
                .Select(r => (r.Accepted, r.Error, r.CommandType, r.PhaseAfter, r.MatchFinished))
                .ToList();
        }
    }
}
