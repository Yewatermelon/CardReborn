using NUnit.Framework;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T6 攻击命令校验测试。</summary>
    [TestFixture]
    public sealed class RuleEngineAttackTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        private static MatchState BuildState(int activePlayerId = 0)
        {
            return RuleEngineTestHelpers.BuildState(activePlayerId, TurnPhase.Main);
        }

        [Test]
        public void Validate_Attack_WhenAttackerNotOnBoard_ReturnsAttackerNotOnBoard()
        {
            MatchState state = BuildState();
            AttackCommand cmd = new AttackCommand(0, attackerInstanceId: 999, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.AttackerNotOnBoard));
        }

        [Test]
        public void Validate_Attack_WhenSummoningSick_ReturnsSummoningSickness()
        {
            MatchState state = BuildState();
            CardInstance attacker = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0, summoningSick: true);
            AttackCommand cmd = new AttackCommand(0, attacker.InstanceId, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.SummoningSickness));
        }

        [Test]
        public void Validate_Attack_WhenChargeIgnoresSummoningSickness_ReturnsValid()
        {
            MatchState state = BuildState();
            CardInstance attacker = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), Db.RequireCard("M3_CHARGE"), instanceId: 0, summoningSick: true);
            AttackCommand cmd = new AttackCommand(0, attacker.InstanceId, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_Attack_WhenAlreadyAttacked_ReturnsAlreadyAttacked()
        {
            MatchState state = BuildState();
            CardInstance attacker = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0, summoningSick: false);
            attacker.AttacksUsedThisTurn = 1;
            AttackCommand cmd = new AttackCommand(0, attacker.InstanceId, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.AlreadyAttacked));
        }

        [Test]
        public void Validate_Attack_WhenTargetNotExists_ReturnsInvalidTarget()
        {
            MatchState state = BuildState();
            CardInstance attacker = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0, summoningSick: false);
            AttackCommand cmd = new AttackCommand(0, attacker.InstanceId, TargetRef.ForMinion(999));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.InvalidTarget));
        }

        [Test]
        public void Validate_Attack_WhenMustTargetTaunt_ReturnsMustTargetTaunt()
        {
            MatchState state = BuildState();
            CardInstance attacker = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0, summoningSick: false);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), Db.RequireCard("M2_TAUNT"), instanceId: 10, summoningSick: false);
            AttackCommand cmd = new AttackCommand(0, attacker.InstanceId, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.MustTargetTaunt));
        }

        [Test]
        public void Validate_Attack_WhenValidAttackOnTaunt_ReturnsValid()
        {
            MatchState state = BuildState();
            CardInstance attacker = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0, summoningSick: false);
            CardInstance taunt = RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), Db.RequireCard("M2_TAUNT"), instanceId: 10, summoningSick: false);
            AttackCommand cmd = new AttackCommand(0, attacker.InstanceId, TargetRef.ForMinion(taunt.InstanceId));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.IsValid, Is.True);
        }
    }
}
