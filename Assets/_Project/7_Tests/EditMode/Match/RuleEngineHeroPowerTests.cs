using NUnit.Framework;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T6 英雄技能 + 结束回合校验测试。</summary>
    [TestFixture]
    public sealed class RuleEngineHeroPowerTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        private static MatchState BuildState(int activePlayerId = 0, TurnPhase phase = TurnPhase.Main)
        {
            return RuleEngineTestHelpers.BuildState(activePlayerId, phase);
        }

        [Test]
        public void Validate_HeroPower_WhenAlreadyUsed_ReturnsHeroPowerAlreadyUsed()
        {
            MatchState state = BuildState();
            state.GetPlayer(0).Hero.PowerUsedThisTurn = true;
            UseHeroPowerCommand cmd = new UseHeroPowerCommand(0, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.HeroPowerAlreadyUsed));
        }

        [Test]
        public void Validate_HeroPower_WhenNotEnoughMana_ReturnsNotEnoughMana()
        {
            MatchState state = BuildState();
            state.GetPlayer(0).Mana.Spend(10); // 0 法力，技能 2 费
            UseHeroPowerCommand cmd = new UseHeroPowerCommand(0, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.NotEnoughMana));
        }

        [Test]
        public void Validate_HeroPower_WhenValid_ReturnsValid()
        {
            MatchState state = BuildState();
            UseHeroPowerCommand cmd = new UseHeroPowerCommand(0, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_EndTurn_WhenValid_ReturnsValid()
        {
            MatchState state = BuildState();
            EndTurnCommand cmd = new EndTurnCommand(0);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_EndTurn_WhenPhaseNotMain_ReturnsNotYourTurn()
        {
            MatchState state = BuildState(phase: TurnPhase.TurnEnd);
            EndTurnCommand cmd = new EndTurnCommand(0);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.NotYourTurn));
        }
    }
}
