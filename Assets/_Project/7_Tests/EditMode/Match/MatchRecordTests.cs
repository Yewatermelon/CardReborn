using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Card.Core;
using Card.Domain.Match;
using Card.Application.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M14-T5 先红测试：MatchRecord 聚合对局记录。</summary>
    [TestFixture]
    public sealed class MatchRecordTests
    {
        private static MatchOutcome BuildOutcome(int startTick = 1000, int endTick = 1500)
        {
            return new MatchOutcome(
                id: "abc123def4567890abc123def4567890",
                result: MatchResult.Player0Wins,
                winnerId: 0,
                turnNumber: 5,
                reason: "座 1 英雄归零",
                startTick: startTick,
                endTick: endTick);
        }

        private static IReadOnlyList<MatchStepRecord> BuildSteps()
        {
            var list = new List<MatchStepRecord>
            {
                new MatchStepRecord(0, "EndTurnCommand", 0, true, CommandError.None, TurnPhase.Main, false),
                new MatchStepRecord(1, "PlayCardCommand", 1, true, CommandError.None, TurnPhase.Main, false),
                new MatchStepRecord(2, "AttackCommand", 0, true, CommandError.None, TurnPhase.Main, false),
                new MatchStepRecord(3, "EndTurnCommand", 1, true, CommandError.None, TurnPhase.MatchEnd, true),
            };
            return list;
        }

        [Test]
        public void Constructor_AggregatesAllFields()
        {
            MatchOutcome outcome = BuildOutcome();
            var names = new[] { "玩家 A", "玩家 B" };

            var record = new MatchRecord(outcome, BuildSteps(), names, endReasonNet: null);

            Assert.That(record.Outcome.MatchId, Is.EqualTo(outcome.MatchId));
            Assert.That(record.Steps.Count, Is.EqualTo(4));
            Assert.That(record.PlayerNames, Is.EqualTo(names));
            Assert.That(record.EndReasonNet, Is.Null);
        }

        [Test]
        public void Constructor_WithEndReasonNet_PreservesIt()
        {
            var record = new MatchRecord(BuildOutcome(), BuildSteps(),
                new[] { "A", "B" }, endReasonNet: "Disconnect");

            Assert.That(record.EndReasonNet, Is.EqualTo("Disconnect"));
        }

        [Test]
        public void Constructor_WithEmptySteps_StillValid()
        {
            var record = new MatchRecord(BuildOutcome(),
                new List<MatchStepRecord>(), new[] { "A", "B" }, null);

            Assert.That(record.Steps.Count, Is.EqualTo(0));
            Assert.That(record.Outcome.TurnNumber, Is.EqualTo(5));
        }
    }
}
