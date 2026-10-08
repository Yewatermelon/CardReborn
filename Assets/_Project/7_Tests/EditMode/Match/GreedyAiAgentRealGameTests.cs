using Card.Application.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T2 AC-2/AC-3/AC-4：双 GreedyAiAgent 真实配置整局——
    /// 零非法命令、有限步内终局、同种子命令序列逐位一致。
    /// </summary>
    [TestFixture]
    public class GreedyAiAgentRealGameTests
    {
        /// <summary>整局命令数硬上限（疲劳终局口径 67 步 × AI 每回合多命令仍远低于此）。</summary>
        private const int MaxTotalSubmissions = 2000;

        [Test]
        public void RealGame_Seed1_FinishesWithZeroInvalid()
        {
            AssertSeedFinishesClean(seed: 1);
        }

        [Test]
        public void RealGame_Seed7_FinishesWithZeroInvalid()
        {
            AssertSeedFinishesClean(seed: 7);
        }

        [Test]
        public void RealGame_Seed20261008_FinishesWithZeroInvalid()
        {
            AssertSeedFinishesClean(seed: 20261008);
        }

        [Test]
        public void RealGame_SameSeed_CommandSequenceIsDeterministic()
        {
            RecordingAuthority first = GreedyAiAgentFixtures.RunRealGame(42, out _);
            RecordingAuthority second = GreedyAiAgentFixtures.RunRealGame(42, out _);

            CollectionAssert.AreEqual(first.Signatures, second.Signatures,
                "同种子两局的命令序列（类型+座位+目标）必须逐位一致。");
        }

        private static void AssertSeedFinishesClean(int seed)
        {
            RecordingAuthority recorder = GreedyAiAgentFixtures.RunRealGame(seed, out MatchController controller);

            Assert.That(controller.IsFinished, Is.True, "种子 " + seed + " 未在有限命令内终局。");
            Assert.That(recorder.InvalidCount, Is.EqualTo(0),
                "种子 " + seed + " 出现被拒命令，AI 预校验与 RuleEngine 口径不一致。");
            Assert.That(recorder.Signatures.Count, Is.LessThan(MaxTotalSubmissions),
                "种子 " + seed + " 提交数异常，疑似死循环。");
        }
    }
}
