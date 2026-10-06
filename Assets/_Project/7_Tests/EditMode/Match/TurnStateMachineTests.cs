using System;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M4-T1：回合阶段状态机——Docs/01 §3.2 的阶段顺序、回环、终局边与非法阶段操作拒绝。
    /// </summary>
    [TestFixture]
    public sealed class TurnStateMachineTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        private static MatchState BuildState(TurnPhase phase = TurnPhase.MatchStart)
        {
            return RuleEngineTestHelpers.BuildState(activePlayerId: 0, phase: phase);
        }

        [Test]
        public void Ctor_WhenMatchStart_CurrentPhaseIsMatchStart()
        {
            MatchState state = BuildState(TurnPhase.MatchStart);

            TurnStateMachine machine = new TurnStateMachine(state);

            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.MatchStart));
        }

        [Test]
        public void Ctor_WhenStateNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TurnStateMachine(null!));
        }

        [Test]
        public void Ctor_WhenPhaseUndefined_Throws()
        {
            MatchState state = BuildState();
            state.Phase = (TurnPhase)999;

            Assert.Throws<ArgumentException>(() => new TurnStateMachine(state));
        }

        [Test]
        public void Advance_FollowsCanonicalOrder_AndLoopsTurnEndBackToTurnStart()
        {
            TurnStateMachine machine = new TurnStateMachine(BuildState(TurnPhase.MatchStart));

            Assert.That(machine.Advance().IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.TurnStart));
            Assert.That(machine.Advance().IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Draw));
            Assert.That(machine.Advance().IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Main));
            Assert.That(machine.Advance().IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.TurnEnd));
            Assert.That(machine.Advance().IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.TurnStart));
        }

        [Test]
        public void MoveTo_WhenLegal_SucceedsAndSyncsMatchStatePhase()
        {
            MatchState state = BuildState(TurnPhase.Main);
            TurnStateMachine machine = new TurnStateMachine(state);

            Result result = machine.MoveTo(TurnPhase.TurnEnd);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.TurnEnd));
            Assert.That(state.Phase, Is.EqualTo(TurnPhase.TurnEnd));
        }

        [TestCase(TurnPhase.MatchStart)]
        [TestCase(TurnPhase.TurnStart)]
        [TestCase(TurnPhase.Draw)]
        [TestCase(TurnPhase.Main)]
        [TestCase(TurnPhase.TurnEnd)]
        public void EndMatch_FromEveryNonTerminalPhase_SucceedsAndSyncs(TurnPhase phase)
        {
            MatchState state = BuildState(phase);
            TurnStateMachine machine = new TurnStateMachine(state);

            Result result = machine.EndMatch();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.MatchEnd));
            Assert.That(state.Phase, Is.EqualTo(TurnPhase.MatchEnd));
        }

        [TestCase(TurnPhase.MatchStart, TurnPhase.Main)]
        [TestCase(TurnPhase.MatchStart, TurnPhase.Draw)]
        [TestCase(TurnPhase.Draw, TurnPhase.TurnEnd)]
        [TestCase(TurnPhase.Main, TurnPhase.TurnStart)]
        [TestCase(TurnPhase.TurnStart, TurnPhase.MatchStart)]
        [TestCase(TurnPhase.TurnEnd, TurnPhase.Draw)]
        public void MoveTo_WhenEdgeNotDeclared_ReturnsIllegalTransitionAndChangesNothing(
            TurnPhase from,
            TurnPhase to)
        {
            MatchState state = BuildState(from);
            TurnStateMachine machine = new TurnStateMachine(state);

            Result result = machine.MoveTo(to);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode,
                Is.EqualTo(StateMachine<TurnPhase>.ErrorIllegalTransition));
            Assert.That(machine.CurrentPhase, Is.EqualTo(from));
            Assert.That(state.Phase, Is.EqualTo(from));
        }

        [TestCase(TurnPhase.Main)]
        [TestCase(TurnPhase.Draw)]
        public void MoveTo_WhenSelfTransitionNotDeclared_IsRejected(TurnPhase phase)
        {
            MatchState state = BuildState(phase);
            TurnStateMachine machine = new TurnStateMachine(state);

            Result result = machine.MoveTo(phase);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(phase));
            Assert.That(state.Phase, Is.EqualTo(phase));
        }

        [Test]
        public void Advance_AtMatchEnd_ReturnsFailureAndStaysAtMatchEnd()
        {
            MatchState state = BuildState(TurnPhase.MatchEnd);
            TurnStateMachine machine = new TurnStateMachine(state);

            Result result = machine.Advance();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.MatchEnd));
            Assert.That(state.Phase, Is.EqualTo(TurnPhase.MatchEnd));
        }

        [Test]
        public void EndMatch_AtMatchEnd_ReturnsFailure()
        {
            TurnStateMachine machine = new TurnStateMachine(BuildState(TurnPhase.MatchEnd));

            Assert.That(machine.EndMatch().IsFailure, Is.True);
        }

        [Test]
        public void MoveTo_FromMatchEnd_ToAnyPhase_IsRejected()
        {
            MatchState state = BuildState(TurnPhase.MatchEnd);
            TurnStateMachine machine = new TurnStateMachine(state);

            foreach (TurnPhase target in Enum.GetValues(typeof(TurnPhase)))
            {
                Assert.That(machine.MoveTo(target).IsFailure, Is.True,
                    "MatchEnd 不允许任何出边：" + target);
            }

            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.MatchEnd));
        }

        [Test]
        public void CanMoveTo_ReflectsDeclaredEdgesWithoutSideEffects()
        {
            MatchState state = BuildState(TurnPhase.Main);
            TurnStateMachine machine = new TurnStateMachine(state);

            Assert.That(machine.CanMoveTo(TurnPhase.TurnEnd), Is.True);
            Assert.That(machine.CanMoveTo(TurnPhase.MatchEnd), Is.True);
            Assert.That(machine.CanMoveTo(TurnPhase.Main), Is.False);
            Assert.That(machine.CanMoveTo(TurnPhase.Draw), Is.False);
            Assert.That(machine.CanMoveTo((TurnPhase)999), Is.False);

            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Main));
            Assert.That(state.Phase, Is.EqualTo(TurnPhase.Main));
        }

        [Test]
        public void MoveTo_WhenIllegalThenLegal_RecoversCleanly()
        {
            MatchState state = BuildState(TurnPhase.Draw);
            TurnStateMachine machine = new TurnStateMachine(state);

            Assert.That(machine.MoveTo(TurnPhase.MatchStart).IsFailure, Is.True);
            Assert.That(machine.MoveTo(TurnPhase.Main).IsSuccess, Is.True);
            Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Main));
            Assert.That(state.Phase, Is.EqualTo(TurnPhase.Main));
        }

        [Test]
        public void Ctor_WhenStateProducedByFactoryShape_EdgesAreAvailableFromMain()
        {
            MatchState state = BuildState(TurnPhase.Main);
            TurnStateMachine machine = new TurnStateMachine(state);

            Assert.That(machine.CanMoveTo(TurnPhase.TurnEnd), Is.True);
            Assert.That(machine.CanMoveTo(TurnPhase.MatchEnd), Is.True);
            Assert.That(machine.CanMoveTo(TurnPhase.TurnStart), Is.False);
            Assert.That(machine.CanMoveTo(TurnPhase.Draw), Is.False);
        }

        [Test]
        public void RuleEngine_RejectsCommandsOutsideMain_AndAcceptsWhenPhaseReturnsToMain()
        {
            MatchState state = BuildState(TurnPhase.Main);
            TurnStateMachine machine = new TurnStateMachine(state);
            EndTurnCommand endTurn = new EndTurnCommand(0);

            Assert.That(machine.MoveTo(TurnPhase.TurnEnd).IsSuccess, Is.True);
            CommandResult rejected = RuleEngine.Validate(state, endTurn, Db);
            Assert.That(rejected.IsValid, Is.False);
            Assert.That(rejected.Error, Is.EqualTo(CommandError.NotYourTurn));

            MatchState fresh = BuildState(TurnPhase.MatchStart);
            TurnStateMachine another = new TurnStateMachine(fresh);
            another.Advance();
            another.Advance();
            Assert.That(another.Advance().IsSuccess, Is.True);
            Assert.That(fresh.Phase, Is.EqualTo(TurnPhase.Main));

            CommandResult accepted = RuleEngine.Validate(fresh, endTurn, Db);
            Assert.That(accepted.IsValid, Is.True);
        }

        [Test]
        public void Transitions_HaveNoBusinessSideEffects()
        {
            MatchState state = BuildState(TurnPhase.MatchStart);
            int activeBefore = state.ActivePlayerId;
            int turnBefore = state.TurnNumber;
            int mana0Before = state.GetPlayer(0).Mana.Current;
            int mana1Before = state.GetPlayer(1).Mana.Current;
            bool finishedBefore = state.IsFinished;

            TurnStateMachine machine = new TurnStateMachine(state);
            for (int i = 0; i < 5; i++)
            {
                machine.Advance();
            }

            machine.EndMatch();

            Assert.That(state.ActivePlayerId, Is.EqualTo(activeBefore));
            Assert.That(state.TurnNumber, Is.EqualTo(turnBefore));
            Assert.That(state.GetPlayer(0).Mana.Current, Is.EqualTo(mana0Before));
            Assert.That(state.GetPlayer(1).Mana.Current, Is.EqualTo(mana1Before));
            Assert.That(state.IsFinished, Is.EqualTo(finishedBefore));
        }
    }
}
