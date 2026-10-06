using System;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T2 AC-5/6/7/8/10/14：拒绝路径、结算接缝、队列顺序、空参与终局后命令。</summary>
    [TestFixture]
    public sealed class MatchControllerRejectTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        [Test]
        public void Ctor_NullArguments_Throw()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();

            Assert.Throws<ArgumentNullException>(() => new MatchController(null!, Db));
            Assert.Throws<ArgumentNullException>(() => new MatchController(state, null!));
        }

        [Test]
        public void Enqueue_Null_Throws()
        {
            MatchController controller =
                new MatchController(RuleEngineTestHelpers.BuildState(), Db);

            Assert.Throws<ArgumentNullException>(() => controller.Enqueue(null!));
        }

        [Test]
        public void ProcessNext_WhenQueueEmpty_Throws()
        {
            MatchController controller =
                new MatchController(RuleEngineTestHelpers.BuildState(), Db);

            Assert.Throws<InvalidOperationException>(() => controller.ProcessNext());
        }

        [Test]
        public void RegisterSettler_Null_Throws()
        {
            MatchController controller =
                new MatchController(RuleEngineTestHelpers.BuildState(), Db);

            Assert.Throws<ArgumentNullException>(() => controller.RegisterSettler(null!));
        }

        [Test]
        public void Submit_WrongSeatEndTurn_RejectedAndStateUnchanged()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);

            CommandResult result = controller.Submit(new EndTurnCommand(1));

            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandError.NotYourTurn));
            Assert.That(state.ActivePlayerId, Is.EqualTo(0));
            Assert.That(state.TurnNumber, Is.EqualTo(1));
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.History[0].Accepted, Is.False);
            Assert.That(controller.History[0].Error, Is.EqualTo(CommandError.NotYourTurn));
            Assert.That(controller.History[0].Sequence, Is.EqualTo(0));
            Assert.That(controller.History[0].MatchFinished, Is.False);
        }

        [Test]
        public void Submit_UnknownCommand_RejectedWithUnknownCommand()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();
            MatchController controller = new MatchController(state, Db);

            CommandResult result = controller.Submit(new BogusCommand());

            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandError.UnknownCommand));
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(controller.History[0].Accepted, Is.False);
        }

        [Test]
        public void Submit_ValidPlayCard_SucceedsAndMutatesState()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            CardInstance card = RuleEngineTestHelpers.AddToHand(
                state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 500);
            int handBefore = state.GetPlayer(0).Hand.Count;
            int manaBefore = state.GetPlayer(0).Mana.Current;
            MatchController controller = new MatchController(state, Db);

            CommandResult result = controller.Submit(
                new PlayCardCommand(0, card.InstanceId, TargetRef.None));

            Assert.That(result.IsValid, Is.True);
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(state.GetPlayer(0).Hand.Count, Is.EqualTo(handBefore - 1));
            Assert.That(state.GetPlayer(0).Mana.Current, Is.EqualTo(manaBefore - Db.RequireCard("M1").Cost));
            Assert.That(state.GetPlayer(0).Board.Count, Is.EqualTo(1));
            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.History[0].Accepted, Is.True);
        }

        [Test]
        public void Queue_ProcessesInFifoOrder_RejectedThenAccepted()
        {
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            MatchController controller = new MatchController(state, Db);
            controller.Enqueue(new EndTurnCommand(1));   // 非法：不是其回合
            controller.Enqueue(new EndTurnCommand(0));   // 合法

            int processed = controller.ProcessPending();

            Assert.That(processed, Is.EqualTo(2));
            Assert.That(controller.PendingCount, Is.EqualTo(0));
            Assert.That(controller.History.Count, Is.EqualTo(2));
            Assert.That(controller.History[0].Accepted, Is.False);
            Assert.That(controller.History[0].PlayerId, Is.EqualTo(1));
            Assert.That(controller.History[1].Accepted, Is.True);
            Assert.That(controller.History[1].PlayerId, Is.EqualTo(0));
            Assert.That(controller.History[1].Sequence, Is.EqualTo(1));
            Assert.That(state.ActivePlayerId, Is.EqualTo(1));
            Assert.That(state.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void Submit_AfterMatchFinished_RejectedAndDoesNotReopenMatch()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();
            state.Phase = TurnPhase.MatchEnd;
            state.IsFinished = true;
            MatchController controller = new MatchController(state, Db);

            CommandResult result = controller.Submit(new EndTurnCommand(0));

            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandError.InvalidTarget));
            Assert.That(controller.IsFinished, Is.True);
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.MatchEnd));
            Assert.That(controller.LastOutcome, Is.Null);
            Assert.That(controller.History.Count, Is.EqualTo(1));
            Assert.That(controller.History[0].Accepted, Is.False);
        }

        private readonly struct BogusCommand : IGameCommand
        {
            public int PlayerId => 0;
        }
    }
}
