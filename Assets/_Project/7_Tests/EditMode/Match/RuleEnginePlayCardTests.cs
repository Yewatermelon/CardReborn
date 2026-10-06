using NUnit.Framework;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T6 出牌命令校验测试。</summary>
    [TestFixture]
    public sealed class RuleEnginePlayCardTests
    {
        private static readonly CardDatabase Db = RuleEngineTestHelpers.BuildDatabase();

        private static MatchState BuildState(int activePlayerId = 0, TurnPhase phase = TurnPhase.Main)
        {
            return RuleEngineTestHelpers.BuildState(activePlayerId, phase);
        }

        [Test]
        public void Validate_PlayCard_WhenNotActivePlayer_ReturnsNotYourTurn()
        {
            MatchState state = BuildState(activePlayerId: 1);
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.NotYourTurn));
        }

        [Test]
        public void Validate_PlayCard_WhenPhaseNotMain_ReturnsNotYourTurn()
        {
            MatchState state = BuildState(phase: TurnPhase.TurnStart);
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.NotYourTurn));
        }

        [Test]
        public void Validate_PlayCard_WhenCardNotInHand_ReturnsCardNotInHand()
        {
            MatchState state = BuildState();
            PlayCardCommand cmd = new PlayCardCommand(0, cardInstanceId: 999, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.CardNotInHand));
        }

        [Test]
        public void Validate_PlayCard_WhenNotEnoughMana_ReturnsNotEnoughMana()
        {
            MatchState state = BuildState();
            state.GetPlayer(0).Mana.Spend(10); // 0 法力
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0); // 2 费
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.NotEnoughMana));
        }

        [Test]
        public void Validate_PlayCard_WhenBoardFull_ReturnsBoardFull()
        {
            MatchState state = BuildState();
            PlayerState p0 = state.GetPlayer(0);
            for (int i = 0; i < p0.Board.Capacity!.Value; i++)
            {
                RuleEngineTestHelpers.AddToBoard(p0, Db.RequireCard("M1"), instanceId: i);
            }
            CardInstance card = RuleEngineTestHelpers.AddToHand(p0, Db.RequireCard("M1"), instanceId: 100);
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.BoardFull));
        }

        [Test]
        public void Validate_PlayCard_WhenTargetRequiredButNone_ReturnsTargetRequired()
        {
            MatchState state = BuildState();
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("S2_TARGET_ANY"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.TargetRequired));
        }

        [Test]
        public void Validate_PlayCard_WhenTargetRuleMismatch_ReturnsInvalidTarget()
        {
            MatchState state = BuildState();
            CardInstance spell = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("S3_TARGET_ENEMY"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, spell.InstanceId, TargetRef.ForHero(0)); // 目标是己方，规则要求敌方

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.Error, Is.EqualTo(CommandError.InvalidTarget));
        }

        [Test]
        public void Validate_PlayCard_WhenValidMinion_ReturnsValid()
        {
            MatchState state = BuildState();
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, card.InstanceId, TargetRef.None);

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_PlayCard_WhenValidSpellWithTarget_ReturnsValid()
        {
            MatchState state = BuildState();
            CardInstance spell = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("S2_TARGET_ANY"), instanceId: 0);
            PlayCardCommand cmd = new PlayCardCommand(0, spell.InstanceId, TargetRef.ForHero(1));

            CommandResult result = Card.Application.Match.RuleEngine.Validate(state, cmd, Db);

            Assert.That(result.IsValid, Is.True);
        }
    }
}
