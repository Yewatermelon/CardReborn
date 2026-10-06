using System.Collections.Generic;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T2 AC-1/2/3/4：初始态与单步回合流（切换/抽牌/法力/TurnStart 簿记）。</summary>
    [TestFixture]
    public sealed class MatchControllerTurnFlowTests
    {
        private CardDatabase _fullDb = null!;
        private IReadOnlyList<string> _deck = null!;

        [SetUp]
        public void Setup()
        {
            _fullDb = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        [Test]
        public void Ctor_InitialState_IsMainTurnOneEmptyLedger()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, seed: 1);
            MatchController controller = new MatchController(match, _fullDb);

            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(controller.IsFinished, Is.False);
            Assert.That(controller.LastOutcome, Is.Null);
            Assert.That(controller.PendingCount, Is.EqualTo(0));
            Assert.That(controller.History.Count, Is.EqualTo(0));
        }

        [Test]
        public void EndTurn_SwitchesActive_DrawsOne_RefillsMana_RecordsAccepted()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, seed: 123);
            int first = match.ActivePlayerId;
            int second = 1 - first;
            PlayerState firstPlayer = match.GetPlayer(first);
            PlayerState secondPlayer = match.GetPlayer(second);
            int secondDeckBefore = secondPlayer.Deck.Count;

            MatchController controller = new MatchController(match, _fullDb);

            CommandResult result = controller.Submit(new EndTurnCommand(first));

            Assert.That(result.IsValid, Is.True);
            Assert.That(match.ActivePlayerId, Is.EqualTo(second));
            Assert.That(match.TurnNumber, Is.EqualTo(2));
            Assert.That(controller.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(secondPlayer.Hand.Count, Is.EqualTo(6));
            Assert.That(secondPlayer.Deck.Count, Is.EqualTo(secondDeckBefore - 1));
            Assert.That(secondPlayer.Mana.Max, Is.EqualTo(1));
            Assert.That(secondPlayer.Mana.Current, Is.EqualTo(1));
            Assert.That(firstPlayer.Mana.Max, Is.EqualTo(1));
            Assert.That(firstPlayer.Mana.Current, Is.EqualTo(1));

            Assert.That(controller.History.Count, Is.EqualTo(1));
            MatchStepRecord record = controller.History[0];
            Assert.That(record.Sequence, Is.EqualTo(0));
            Assert.That(record.CommandType, Is.EqualTo(nameof(EndTurnCommand)));
            Assert.That(record.PlayerId, Is.EqualTo(first));
            Assert.That(record.Accepted, Is.True);
            Assert.That(record.Error, Is.EqualTo(CommandError.None));
            Assert.That(record.PhaseAfter, Is.EqualTo(TurnPhase.Main));
            Assert.That(record.MatchFinished, Is.False);
        }

        [Test]
        public void TurnStart_ResetsPowerAndAttacksAndSickness_OnlyForNewActive()
        {
            CardDatabase db = RuleEngineTestHelpers.BuildDatabase();
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 1);
            CardDefinition minionDef = db.RequireCard("M1");
            CardInstance p0Minion = RuleEngineTestHelpers.AddToBoard(
                state.GetPlayer(0), minionDef, instanceId: 101, summoningSick: true);
            p0Minion.AttacksUsedThisTurn = 1;
            p0Minion.Statuses.Add(StatusFlags.DivineShield);
            state.GetPlayer(0).Hero.PowerUsedThisTurn = true;

            CardInstance p1Minion = RuleEngineTestHelpers.AddToBoard(
                state.GetPlayer(1), minionDef, instanceId: 202, summoningSick: true);
            p1Minion.AttacksUsedThisTurn = 1;

            MatchController controller = new MatchController(state, db);

            Assert.That(controller.Submit(new EndTurnCommand(1)).IsValid, Is.True);

            Assert.That(state.ActivePlayerId, Is.EqualTo(0));
            Assert.That(state.GetPlayer(0).Hero.PowerUsedThisTurn, Is.False);
            Assert.That(p0Minion.AttacksUsedThisTurn, Is.EqualTo(0));
            Assert.That(p0Minion.Statuses.Has(StatusFlags.SummoningSickness), Is.False);
            Assert.That(p0Minion.Statuses.Has(StatusFlags.DivineShield), Is.True);
            Assert.That(p1Minion.AttacksUsedThisTurn, Is.EqualTo(1));
            Assert.That(p1Minion.Statuses.Has(StatusFlags.SummoningSickness), Is.True);

            // 座 0 空牌库在 Draw 阶段受 1 点疲劳——同时锁定 Draw 阶段确实执行。
            Assert.That(state.GetPlayer(0).FatigueCounter, Is.EqualTo(1));
            Assert.That(state.GetPlayer(0).Hero.Health, Is.EqualTo(29));
        }

        [Test]
        public void Mana_GrowsAndRefills_OnEachPlayerTurn()
        {
            MatchState match = MatchControllerFixtures.NewMatch(_fullDb, _deck, seed: 77);
            int first = match.ActivePlayerId;
            int second = 1 - first;
            MatchController controller = new MatchController(match, _fullDb);

            controller.Submit(new EndTurnCommand(first));   // → 回合 2：后手
            controller.Submit(new EndTurnCommand(second));  // → 回合 3：先手
            controller.Submit(new EndTurnCommand(first));   // → 回合 4：后手

            Assert.That(match.GetPlayer(first).Mana.Max, Is.EqualTo(2));
            Assert.That(match.GetPlayer(first).Mana.Current, Is.EqualTo(2));
            Assert.That(match.GetPlayer(second).Mana.Max, Is.EqualTo(2));
            Assert.That(match.GetPlayer(second).Mana.Current, Is.EqualTo(2));
        }
    }
}
