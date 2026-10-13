using System.Collections.Generic;
using System.Linq;
using Card.Domain.Config;
using Card.Network;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// M14-T7：NetworkSoakRunner 多局回归（真实 TCP 回环 + 双座位合法策略）。
    /// AC-1/2/3：全对局正常终局、建拆重建无残留、策略零拒绝；防死循环护栏可续跑。
    /// 共享报告由 OneTimeSetUp 跑一次 30 局回归（决策点实测 30 局约 24s < 60s，
    /// 门禁直接采用 30 局，无需 [Explicit] 拆分入口）。
    /// </summary>
    [TestFixture]
    public class NetworkSoakRunnerTests
    {
        private CardDatabase _database = null!;
        private NetworkSoakReport _report = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _report = NetworkSoakRunner.Run(_database, new NetworkSoakOptions
            {
                MatchCount = 30,
                BaseSeed = 424242,
                MaxStepsPerMatch = 3000,
                MaxPumpsPerWait = 2000,
            });
        }

        [Test]
        public void Run_ThirtyMatches_AllFinished()
        {
            Assert.That(_report.Matches.Count, Is.EqualTo(30));
            Assert.That(_report.AllFinished, Is.True, "存在未终局对局：" + Describe(_report));
            foreach (NetworkSoakMatchRecord record in _report.Matches)
            {
                Assert.That(record.Finished, Is.True);
                Assert.That(record.Failure, Is.Null);
                Assert.That(record.SubmissionCount, Is.GreaterThan(0));
                Assert.That(record.TurnNumber, Is.GreaterThan(0));
                Assert.That(record.DisconnectCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void Run_FinishedMatches_WinnerInRange()
        {
            foreach (NetworkSoakMatchRecord record in _report.Matches)
            {
                Assert.That(record.WinnerId, Is.Null.Or.InRange(0, 1),
                    "胜者必须是座位 0/1 或平局（null）：" + Describe(_report));
            }
        }

        [Test]
        public void Run_PolicyLegal_RejectedZero()
        {
            Assert.That(_report.Matches.Sum(m => m.RejectedCount), Is.EqualTo(0),
                "SoakPolicy 只发合法命令，不应出现被拒命令：" + Describe(_report));
            Assert.That(_report.Matches.Sum(m => m.HeartbeatLoops), Is.GreaterThan(0),
                "网络 tick 应被实际驱动过。");
        }

        [Test]
        public void Report_FinishedCountAggregates()
        {
            NetworkSoakReport report = new NetworkSoakReport(new List<NetworkSoakMatchRecord>
            {
                new NetworkSoakMatchRecord { Finished = true },
                new NetworkSoakMatchRecord { Finished = true },
                new NetworkSoakMatchRecord { Finished = false },
            });
            Assert.That(report.FinishedCount, Is.EqualTo(2));
            Assert.That(report.AllFinished, Is.False);
        }

        [Test]
        public void Report_AllFinished_FalseWhenAnyFailure()
        {
            NetworkSoakReport good = new NetworkSoakReport(new List<NetworkSoakMatchRecord>
            {
                new NetworkSoakMatchRecord { Finished = true },
                new NetworkSoakMatchRecord { Finished = true },
            });
            NetworkSoakReport bad = new NetworkSoakReport(new List<NetworkSoakMatchRecord>
            {
                new NetworkSoakMatchRecord { Finished = true },
                new NetworkSoakMatchRecord { Finished = true, Failure = "步数上限" },
            });
            Assert.That(good.AllFinished, Is.True);
            Assert.That(bad.AllFinished, Is.False, "任一局带 Failure 即不应判定全部完成。");
        }

        [Test]
        public void Run_MaxStepsGuard_MarksFailureAndContinues()
        {
            NetworkSoakReport report = NetworkSoakRunner.Run(_database, new NetworkSoakOptions
            {
                MatchCount = 2,
                BaseSeed = 777,
                MaxStepsPerMatch = 1,
                MaxPumpsPerWait = 2000,
            });
            Assert.That(report.Matches.Count, Is.EqualTo(2), "护栏触发后应继续跑后续对局。");
            foreach (NetworkSoakMatchRecord record in report.Matches)
            {
                Assert.That(record.Failure, Is.Not.Null, "步数超限应记失败原因。");
                Assert.That(record.Finished, Is.False);
            }

            Assert.That(report.AllFinished, Is.False);
        }

        /// <summary>报告摘要（失败信息用，避免逐局断言信息不足）。</summary>
        private static string Describe(NetworkSoakReport report)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < report.Matches.Count; i++)
            {
                NetworkSoakMatchRecord record = report.Matches[i];
                parts.Add("#" + i + "(seed=" + record.Seed + ",finished=" + record.Finished
                    + ",failure=" + (record.Failure ?? "-")
                    + ",rejected=" + record.RejectedCount
                    + ",firstReject=" + (record.FirstReject ?? "-") + ")");
            }

            return string.Join(",", parts);
        }
    }
}
