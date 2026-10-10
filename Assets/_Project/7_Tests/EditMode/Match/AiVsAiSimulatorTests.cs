using System.Collections.Generic;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T4：AI vs AI 批量模拟器——小批量正确性、确定性、超限容错与 500 局 M7 门禁。
    /// 门禁（Docs/02）：500 局无崩溃、无死循环、无非法操作；胜率落 [45%,55%] 或登记调整记录。
    /// </summary>
    [TestFixture]
    public class AiVsAiSimulatorTests
    {
        private const int GateMatchCount = 500;

        [Test]
        public void Run_SmallBatch_AllFinishWithZeroInvalid()
        {
            AiVsAiSimulationReport report = RunBatch(matchCount: 10);

            Assert.That(report.Failures, Is.EqualTo(0), "小批量不应有失败局：\n" + report.Summary());
            Assert.That(report.Total, Is.EqualTo(10));
            AssertConservation(report);
            foreach (AiVsAiMatchRecord record in report.Matches)
            {
                Assert.That(record.Failure, Is.Null, "seed " + record.Seed + " 意外失败。");
                Assert.That(record.Result, Is.Not.EqualTo(MatchResult.Ongoing), "seed " + record.Seed + " 未终局。");
                Assert.That(record.InvalidCount, Is.EqualTo(0), "seed " + record.Seed + " 出现被拒命令。");
            }
        }

        [Test]
        public void Run_SameOptions_ReportIsDeterministic()
        {
            AiVsAiSimulationReport first = RunBatch(matchCount: 10, baseSeed: 42);
            AiVsAiSimulationReport second = RunBatch(matchCount: 10, baseSeed: 42);

            Assert.That(second.Total, Is.EqualTo(first.Total));
            Assert.That(second.P0Wins, Is.EqualTo(first.P0Wins));
            Assert.That(second.P1Wins, Is.EqualTo(first.P1Wins));
            Assert.That(second.Draws, Is.EqualTo(first.Draws));
            Assert.That(second.Failures, Is.EqualTo(first.Failures));
            Assert.That(second.AverageTurns, Is.EqualTo(first.AverageTurns));
            for (int i = 0; i < first.Matches.Count; i++)
            {
                Assert.That(second.Matches[i].Seed, Is.EqualTo(first.Matches[i].Seed));
                Assert.That(second.Matches[i].Result, Is.EqualTo(first.Matches[i].Result));
                Assert.That(second.Matches[i].TurnNumber, Is.EqualTo(first.Matches[i].TurnNumber));
                Assert.That(second.Matches[i].SubmissionCount, Is.EqualTo(first.Matches[i].SubmissionCount));
            }
        }

        [Test]
        public void Run_TinyStepLimit_RecordsFailureAndContinues()
        {
            AiVsAiSimulationReport report = RunBatch(matchCount: 5, maxSubmissions: 2);

            Assert.That(report.Failures, Is.EqualTo(5), "2 条提交上限下一局必然超限。");
            AssertConservation(report);
            foreach (AiVsAiMatchRecord record in report.Matches)
            {
                Assert.That(record.Failure, Is.Not.Null, "seed " + record.Seed + " 应记录失败原因。");
                Assert.That(record.SubmissionCount, Is.GreaterThan(0), "超限记录应保留提交计数。");
            }
        }

        [Test]
        public void Run_Summary_ContainsWinRateAndAverageTurns()
        {
            AiVsAiSimulationReport report = RunBatch(matchCount: 4);

            string summary = report.Summary();
            Assert.That(summary, Does.Contain("P0").And.Contain("P1"));
            Assert.That(summary, Does.Contain("回合"));
            Assert.That(summary, Does.Contain("异常日志"));
        }

        [Test]
        public void Run_Gate500_ZeroFailureZeroInvalidAllFinished()
        {
            AiVsAiSimulationReport report = RunBatch(GateMatchCount);

            AssertConservation(report);
            Assert.That(report.Failures, Is.EqualTo(0), "M7 门禁：500 局零失败。\n" + report.Summary());
            Assert.That(report.P0Wins + report.P1Wins + report.Draws, Is.EqualTo(GateMatchCount));
            Assert.That(report.AverageTurns, Is.GreaterThan(0).And.LessThanOrEqualTo(60),
                "平均回合数异常，疑似收尾异常或死循环。\n" + report.Summary());
            foreach (AiVsAiMatchRecord record in report.Matches)
            {
                Assert.That(record.InvalidCount, Is.EqualTo(0), "M7 门禁：seed " + record.Seed + " 出现被拒命令。");
            }

            TestContext.Out.WriteLine(report.Summary());
        }

        private static AiVsAiSimulationReport RunBatch(int matchCount, int baseSeed = 1, int maxSubmissions = 2000)
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            return AiVsAiSimulator.Run(
                database,
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                new AiVsAiSimulatorOptions
                {
                    MatchCount = matchCount,
                    BaseSeed = baseSeed,
                    MaxSubmissionsPerMatch = maxSubmissions,
                });
        }

        private static void AssertConservation(AiVsAiSimulationReport report)
        {
            Assert.That(report.P0Wins + report.P1Wins + report.Draws + report.Failures,
                Is.EqualTo(report.Total), "统计不守恒。");
        }
    }
}
