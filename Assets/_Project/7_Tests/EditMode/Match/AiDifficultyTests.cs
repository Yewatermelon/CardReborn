using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M7-T5：AI 难度分级——Profile 单元 + 行为差异 + 向后兼容 + 单调性。</summary>
    public sealed class AiDifficultyTests
    {
        // ---------- AiDifficultyProfile 单元 ----------

        [Test]
        public void Profile_Easy_PresetValues()
        {
            AiDifficultyProfile easy = AiDifficultyProfile.Easy;
            Assert.That(easy.Key, Is.EqualTo("Easy"));
            Assert.That(easy.Policy, Is.EqualTo(AiEvaluationPolicy.Greedy));
            Assert.That(easy.PlayHighCostFirst, Is.False);
            Assert.That(easy.RandomTarget, Is.True);
            Assert.That(easy.MistakeRate, Is.EqualTo(0.3f));
            Assert.That(easy.SearchDepth, Is.EqualTo(0));
        }

        [Test]
        public void Profile_Normal_PresetValues()
        {
            AiDifficultyProfile normal = AiDifficultyProfile.Normal;
            Assert.That(normal.Key, Is.EqualTo("Normal"));
            Assert.That(normal.Policy, Is.EqualTo(AiEvaluationPolicy.Greedy));
            Assert.That(normal.PlayHighCostFirst, Is.True);
            Assert.That(normal.RandomTarget, Is.False);
            Assert.That(normal.MistakeRate, Is.EqualTo(0f));
        }

        [Test]
        public void Profile_Hard_PresetValues()
        {
            AiDifficultyProfile hard = AiDifficultyProfile.Hard;
            Assert.That(hard.Key, Is.EqualTo("Hard"));
            Assert.That(hard.Policy, Is.EqualTo(AiEvaluationPolicy.Evaluated));
            Assert.That(hard.PlayHighCostFirst, Is.True);
            Assert.That(hard.MistakeRate, Is.EqualTo(0f));
        }

        [Test]
        public void Profile_FromKey_KnownKeysReturnPresets()
        {
            Assert.That(AiDifficultyProfile.FromKey("Easy"), Is.SameAs(AiDifficultyProfile.Easy));
            Assert.That(AiDifficultyProfile.FromKey("Normal"), Is.SameAs(AiDifficultyProfile.Normal));
            Assert.That(AiDifficultyProfile.FromKey("Hard"), Is.SameAs(AiDifficultyProfile.Hard));
        }

        [Test]
        public void Profile_FromKey_UnknownFallsBackToNormal()
        {
            Assert.That(AiDifficultyProfile.FromKey("Hardcore"), Is.SameAs(AiDifficultyProfile.Normal));
            Assert.That(AiDifficultyProfile.FromKey(""), Is.SameAs(AiDifficultyProfile.Normal));
        }

        // ---------- 向后兼容：默认构造行为与 T4 基线一致 ----------

        [Test]
        public void BackwardCompat_DefaultGreedyAgent_IdenticalCommandsToExplicitNormal()
        {
            // 不传 difficulty 等价于 Normal——同 seed 命令序列逐位相同。
            const int Seed = 20261010;
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();

            // 默认构造（不传 difficulty）
            MatchState stateA = MatchControllerFixtures.NewMatch(db, deck, Seed);
            var ctrlA = new MatchController(stateA, db);
            var recA = new RecordingAuthority(ctrlA);
            IPlayerAgent[] agentsA =
            {
                new GreedyAiAgent(0, db),
                new GreedyAiAgent(1, db),
            };
            new AgentMatchRunner(recA, ctrlA.View, agentsA).Start();

            // 显式 Normal
            MatchState stateB = MatchControllerFixtures.NewMatch(db, deck, Seed);
            var ctrlB = new MatchController(stateB, db);
            var recB = new RecordingAuthority(ctrlB);
            IPlayerAgent[] agentsB =
            {
                new GreedyAiAgent(0, db, difficulty: AiDifficultyProfile.Normal),
                new GreedyAiAgent(1, db, difficulty: AiDifficultyProfile.Normal),
            };
            new AgentMatchRunner(recB, ctrlB.View, agentsB).Start();

            Assert.That(recA.Signatures, Is.EqualTo(recB.Signatures),
                "默认构造应与显式 Normal 命令序列逐位一致。");
            Assert.That(recA.InvalidCount, Is.EqualTo(0));
            Assert.That(recB.InvalidCount, Is.EqualTo(0));
        }

        // ---------- Easy vs Normal：命令序列不同 ----------

        [Test]
        public void Easy_Commands_DifferFromNormal_RealGame()
        {
            // 多 seed 下，Easy vs Normal 至少 50% seed 命令序列不同。
            int diffSeeds = 0;
            const int SeedCount = 20;
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();

            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var normalRec = RunRealGameWithDifficulty(db, deck, seed, AiDifficultyProfile.Normal);
                var easyRec = RunRealGameWithDifficulty(db, deck, seed, AiDifficultyProfile.Easy);

                if (!easyRec.Signatures.SequenceEqual(normalRec.Signatures))
                {
                    diffSeeds++;
                }
            }

            Assert.That(diffSeeds, Is.GreaterThanOrEqualTo(SeedCount / 2),
                $"Easy 与 Normal 应在多数 seed 下命令序列不同（{diffSeeds}/{SeedCount}）。");
        }

        // ---------- Hard vs Normal：命令序列不同 ----------

        [Test]
        public void Hard_Commands_DifferFromNormal_RealGame()
        {
            int diffSeeds = 0;
            const int SeedCount = 20;
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();

            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var normalRec = RunRealGameWithDifficulty(db, deck, seed, AiDifficultyProfile.Normal);
                var hardRec = RunRealGameWithDifficulty(db, deck, seed, AiDifficultyProfile.Hard);

                if (!hardRec.Signatures.SequenceEqual(normalRec.Signatures))
                {
                    diffSeeds++;
                }
            }

            Assert.That(diffSeeds, Is.GreaterThanOrEqualTo(SeedCount / 2),
                $"Hard 与 Normal 应在多数 seed 下命令序列不同（{diffSeeds}/{SeedCount}）。");
        }

        // ---------- AiVsAiSimulator 支持难度参数 ----------

        [Test]
        public void Simulator_AcceptsDifficultyParameters()
        {
            // Run 可指定 Player0Difficulty / Player1Difficulty，不传则默认 Normal。
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            var setup = new MatchSetupRequest("HERO_MAGE", deck);

            var normalReport = AiVsAiSimulator.Run(db, setup, setup,
                new AiVsAiSimulatorOptions { MatchCount = 5, Player0Difficulty = AiDifficultyProfile.Normal, Player1Difficulty = AiDifficultyProfile.Normal });
            var easyReport = AiVsAiSimulator.Run(db, setup, setup,
                new AiVsAiSimulatorOptions { MatchCount = 5, Player0Difficulty = AiDifficultyProfile.Easy, Player1Difficulty = AiDifficultyProfile.Easy });

            // 两者都能跑完 5 局且零失败
            Assert.That(normalReport.Total, Is.EqualTo(5));
            Assert.That(normalReport.Failures, Is.EqualTo(0));
            Assert.That(easyReport.Total, Is.EqualTo(5));
            Assert.That(easyReport.Failures, Is.EqualTo(0));
        }

        // ---------- 单调性：Easy vs Normal ----------

        [Test]
        public void Monotonic_EasyVsNormal_NormalWinsMore()
        {
            // 同英雄镜像：Player0=Normal, Player1=Easy → Normal 胜率应 > 50%
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            var setup = new MatchSetupRequest("HERO_MAGE", deck);

            var report = AiVsAiSimulator.Run(db, setup, setup,
                new AiVsAiSimulatorOptions
                {
                    MatchCount = 200,
                    Player0Difficulty = AiDifficultyProfile.Normal,
                    Player1Difficulty = AiDifficultyProfile.Easy,
                });

            // Normal (P0) 胜率应 > 50%（排除英雄偏置的镜像对局，Normal 应当胜出）
            double normalWinRate = report.Player0WinRate;
            TestContext.Out.WriteLine("Normal vs Easy 200 局：Normal 胜率 " + (normalWinRate * 100).ToString("F1") + "%，" + report.Summary());
            Assert.That(normalWinRate, Is.GreaterThan(0.5),
                "Normal vs Easy：Normal 胜率应 > 50%（镜像对局排除英雄偏置）。");
            Assert.That(report.Failures, Is.EqualTo(0));
        }

        // ---------- 单调性：Normal vs Hard ----------

        [Test]
        public void Monotonic_NormalVsHard_HardWinsMore()
        {
            // 同英雄镜像：Player0=Hard, Player1=Normal → Hard 胜率应 > 50%
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            var setup = new MatchSetupRequest("HERO_MAGE", deck);

            var report = AiVsAiSimulator.Run(db, setup, setup,
                new AiVsAiSimulatorOptions
                {
                    MatchCount = 200,
                    Player0Difficulty = AiDifficultyProfile.Hard,
                    Player1Difficulty = AiDifficultyProfile.Normal,
                });

            double hardWinRate = report.Player0WinRate;
            TestContext.Out.WriteLine("Hard vs Normal 200 局：Hard 胜率 " + (hardWinRate * 100).ToString("F1") + "%，" + report.Summary());
            Assert.That(hardWinRate, Is.GreaterThan(0.5),
                "Hard vs Normal：Hard 胜率应 > 50%（镜像对局排除英雄偏置）。");
            Assert.That(report.Failures, Is.EqualTo(0));
        }

        // ---------- Helper ----------

        private static RecordingAuthority RunRealGameWithDifficulty(
            CardDatabase db, IReadOnlyList<string> deck, int seed, AiDifficultyProfile diff0)
        {
            MatchState state = MatchControllerFixtures.NewMatch(db, deck, seed);
            var ctrl = new MatchController(state, db);
            var rec = new RecordingAuthority(ctrl);
            IPlayerAgent[] agents =
            {
                new GreedyAiAgent(0, db, difficulty: diff0),
                new GreedyAiAgent(1, db, difficulty: AiDifficultyProfile.Normal),
            };
            new AgentMatchRunner(rec, ctrl.View, agents).Start();
            return rec;
        }
    }
}
