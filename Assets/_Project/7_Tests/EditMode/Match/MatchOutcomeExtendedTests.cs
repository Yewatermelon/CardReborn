using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Card.Core;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M14-T5 先红测试：MatchOutcome 扩展对局元数据（Id / 时间 tick）。</summary>
    [TestFixture]
    public sealed class MatchOutcomeExtendedTests
    {
        private static MatchOutcome Build(int? winnerId = 0, int startTick = 1000, int endTick = 1523)
        {
            return new MatchOutcome(
                id: Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                result: winnerId.HasValue ? MatchResult.Player0Wins : MatchResult.Draw,
                winnerId: winnerId,
                turnNumber: 8,
                reason: "测试原因",
                startTick: startTick,
                endTick: endTick);
        }

        [Test]
        public void Constructor_WithAllFields_PreservesMetadata()
        {
            MatchOutcome o = Build(startTick: 500, endTick: 725);

            Assert.That(o.MatchId, Is.Not.Empty);
            Assert.That(o.StartTick, Is.EqualTo(500));
            Assert.That(o.EndTick, Is.EqualTo(725));
            Assert.That(o.DurationTicks, Is.EqualTo(225));
        }

        [Test]
        public void DurationTicks_WhenEndLessThanStart_ClampedToZero()
        {
            MatchOutcome o = Build(startTick: 500, endTick: 300);

            Assert.That(o.DurationTicks, Is.EqualTo(0));
        }

        [Test]
        public void MatchId_IsValidGuidFormat()
        {
            MatchOutcome o = Build();

            Guid parsed;
            bool ok = Guid.TryParse(o.MatchId, out parsed);
            Assert.That(ok, Is.True);
        }

        [Test]
        public void TwoOutcomes_HaveDistinctMatchIds()
        {
            MatchOutcome a = Build();
            MatchOutcome b = Build();

            Assert.That(a.MatchId, Is.Not.EqualTo(b.MatchId));
        }
    }
}
