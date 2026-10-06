using NUnit.Framework;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T7 失败测试（红）：MatchEvaluator 尚未实现，应先编译失败。</summary>
    [TestFixture]
    public sealed class MatchEvaluatorTests
    {
        private static MatchState BuildState(int p0Health, int p1Health)
        {
            PlayerState p0 = new PlayerState(0, new HeroState("HERO_A", "POWER_A", 30), new ManaPool(max: 1, current: 1));
            PlayerState p1 = new PlayerState(1, new HeroState("HERO_B", "POWER_B", 30), new ManaPool(max: 1, current: 1));
            p0.Hero.Health = p0Health;
            p1.Hero.Health = p1Health;
            return new MatchState(p0, p1, 0) { Phase = TurnPhase.Main, TurnNumber = 5 };
        }

        [Test]
        public void Evaluate_WhenBothHeroesAlive_ReturnsOngoing()
        {
            MatchState state = BuildState(p0Health: 30, p1Health: 20);

            MatchOutcome outcome = Card.Application.Match.MatchEvaluator.Evaluate(state);

            Assert.That(outcome.Result, Is.EqualTo(MatchResult.Ongoing));
            Assert.That(outcome.WinnerId, Is.Null);
        }

        [Test]
        public void Evaluate_WhenPlayer0Dead_ReturnsPlayer1Wins()
        {
            MatchState state = BuildState(p0Health: 0, p1Health: 20);

            MatchOutcome outcome = Card.Application.Match.MatchEvaluator.Evaluate(state);

            Assert.That(outcome.Result, Is.EqualTo(MatchResult.Player1Wins));
            Assert.That(outcome.WinnerId, Is.EqualTo(1));
            Assert.That(outcome.TurnNumber, Is.EqualTo(5));
        }

        [Test]
        public void Evaluate_WhenPlayer1Dead_ReturnsPlayer0Wins()
        {
            MatchState state = BuildState(p0Health: 30, p1Health: 0);

            MatchOutcome outcome = Card.Application.Match.MatchEvaluator.Evaluate(state);

            Assert.That(outcome.Result, Is.EqualTo(MatchResult.Player0Wins));
            Assert.That(outcome.WinnerId, Is.EqualTo(0));
        }

        [Test]
        public void Evaluate_WhenBothDead_ReturnsDraw()
        {
            MatchState state = BuildState(p0Health: 0, p1Health: -3);

            MatchOutcome outcome = Card.Application.Match.MatchEvaluator.Evaluate(state);

            Assert.That(outcome.Result, Is.EqualTo(MatchResult.Draw));
            Assert.That(outcome.WinnerId, Is.Null);
        }

        [Test]
        public void Evaluate_WhenExactlyZeroHealth_TreatedAsDead()
        {
            MatchState state = BuildState(p0Health: 0, p1Health: 1);

            MatchOutcome outcome = Card.Application.Match.MatchEvaluator.Evaluate(state);

            Assert.That(outcome.Result, Is.EqualTo(MatchResult.Player1Wins));
        }

        [Test]
        public void Validate_PlayCard_WhenMatchFinished_ReturnsInvalid()
        {
            CardDatabase db = RuleEngineTestHelpers.BuildDatabase();
            MatchState state = RuleEngineTestHelpers.BuildState();
            state.IsFinished = true;
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard("M1"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, db);

            Assert.That(result.Error, Is.EqualTo(CommandError.InvalidTarget));
            Assert.That(result.Detail, Does.Contain("对局已结束"));
        }

        [Test]
        public void Validate_EndTurn_WhenMatchFinished_ReturnsInvalid()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();
            state.IsFinished = true;
            EndTurnCommand cmd = new EndTurnCommand(0);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, RuleEngineTestHelpers.BuildDatabase());

            Assert.That(result.Error, Is.EqualTo(CommandError.InvalidTarget));
            Assert.That(result.Detail, Does.Contain("对局已结束"));
        }

        [Test]
        public void FatigueCounter_DefaultsToZero()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();

            Assert.That(state.GetPlayer(0).FatigueCounter, Is.EqualTo(0));
            Assert.That(state.GetPlayer(1).FatigueCounter, Is.EqualTo(0));
        }

        [Test]
        public void IsFinished_DefaultsToFalse()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();

            Assert.That(state.IsFinished, Is.False);
        }
    }
}
