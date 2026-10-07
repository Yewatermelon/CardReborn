using NUnit.Framework;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle.Input;
using Card.Tests.EditMode.Match;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>用真实 MatchFactory+MatchController 验证适配器透传语义（不经过 Unity 场景）。</summary>
    [TestFixture]
    public sealed class MatchControllerCommandSinkTests
    {
        [Test]
        public void Submit_ActivePlayerEndTurn_IsAcceptedAndAdvancesTurn()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            System.Collections.Generic.IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(db, deck, seed: 1234);
            var controller = new MatchController(state, db);
            var sink = new MatchControllerCommandSink(controller);
            int activeSeat = state.ActivePlayerId;
            int turnBefore = state.TurnNumber;

            CommandResult result = sink.Submit(new EndTurnCommand(activeSeat));

            Assert.That(result.IsValid, Is.True, "active player end-turn rejected: " + result.Error);
            Assert.That(state.TurnNumber, Is.EqualTo(turnBefore + 1));
        }

        [Test]
        public void Submit_NonActivePlayerEndTurn_IsRejectedWithNotYourTurn()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            System.Collections.Generic.IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(db, deck, seed: 1234);
            var controller = new MatchController(state, db);
            var sink = new MatchControllerCommandSink(controller);
            int nonActive = 1 - state.ActivePlayerId;

            CommandResult result = sink.Submit(new EndTurnCommand(nonActive));

            Assert.That(result.IsInvalid, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandError.NotYourTurn));
        }

        [Test]
        public void Ctor_NullController_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new MatchControllerCommandSink(null!),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("controller"));
        }
    }
}
