using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M6-T4（B5）：出牌结算应给无 Charge 的随从设置召唤失调，
    /// 使其在登场回合不能攻击；Charge 随从免疫该状态。
    /// </summary>
    [TestFixture]
    public sealed class PlayCardSummoningSicknessTests
    {
        private static CardDatabase Db => RuleEngineTestHelpers.BuildDatabase();

        [Test]
        public void PlayCard_NonChargeMinion_GetsSummoningSickness_CannotAttackThisTurn()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("M1"), instanceId: 100);
            MatchController controller = new MatchController(state, Db);

            CommandResult play = controller.Submit(new PlayCardCommand(0, card.InstanceId, TargetRef.None));
            Assert.That(play.IsValid, Is.True, "出牌应被接受：" + play.Error);

            Assert.That(state.GetPlayer(0).Board.Cards[0].Statuses.Has(StatusFlags.SummoningSickness),
                Is.True, "无 Charge 的随从登场应处于召唤失调状态。");

            CommandResult attack = controller.Submit(
                new AttackCommand(0, card.InstanceId, TargetRef.ForHero(1)));
            Assert.That(attack.Error, Is.EqualTo(CommandError.SummoningSickness),
                "无 Charge 随从登场回合不应能攻击。");
        }

        [Test]
        public void PlayCard_ChargeMinion_IgnoresSummoningSickness_CanAttackThisTurn()
        {
            MatchState state = RuleEngineTestHelpers.BuildState();
            CardInstance card = RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), Db.RequireCard("M3_CHARGE"), instanceId: 101);
            MatchController controller = new MatchController(state, Db);

            CommandResult play = controller.Submit(new PlayCardCommand(0, card.InstanceId, TargetRef.None));
            Assert.That(play.IsValid, Is.True, "出牌应被接受：" + play.Error);

            Assert.That(state.GetPlayer(0).Board.Cards[0].Statuses.Has(StatusFlags.SummoningSickness),
                Is.False, "Charge 随从不应处于召唤失调状态。");

            CommandResult attack = controller.Submit(
                new AttackCommand(0, card.InstanceId, TargetRef.ForHero(1)));
            Assert.That(attack.IsValid, Is.True, "Charge 随从登场回合应能攻击：" + attack.Error);
        }
    }
}
