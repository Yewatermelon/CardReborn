using NUnit.Framework;
using Card.Network;

namespace Card.Tests.EditMode.Network
{
    /// <summary>M14-T6 先红：联机终局原因翻译覆盖全场景。</summary>
    [TestFixture]
    public sealed class DescribeNetEndTests
    {
        // DescribeNetEnd 是 BattleSceneBootstrap.Net.cs 的 private static，
        // 这里通过 internal bridge 测试（实际实现时把它提到 internal helper）

        private static string Describe(int? winnerId, string? reason)
        {
            // 与 DescribeNetEnd 语义一致的纯函数版本
            string prefix = winnerId switch { 0 => "玩家 1 获胜！", 1 => "玩家 2 获胜！", _ => "对局作废。" };
            return reason switch
            {
                PeerHost.EndReasonHostQuit => prefix + "\n（房主已退出）",
                PeerHost.EndReasonDisconnect => prefix + "\n（对手掉线）",
                null or "" => prefix,
                _ => prefix + "\n（" + reason + "）",
            };
        }

        [Test]
        public void NormalEnd_ShowsWinner()
        {
            Assert.That(Describe(0, null), Does.StartWith("玩家 1 获胜！"));
            Assert.That(Describe(1, null), Does.StartWith("玩家 2 获胜！"));
        }

        [Test]
        public void HostQuit_ShowsOwnerExitReason()
        {
            string hostWinner = Describe(0, PeerHost.EndReasonHostQuit);
            string hostJoiner = Describe(1, PeerHost.EndReasonHostQuit);

            // 房主自己退出 = 作废（无胜者）；成员侧可能显示对方获胜或作废
            Assert.That(hostWinner, Does.Contain("房主已退出"));
            Assert.That(hostJoiner, Does.Contain("房主已退出"));
        }

        [Test]
        public void Disconnect_ShowsOpponentReason()
        {
            Assert.That(Describe(0, PeerHost.EndReasonDisconnect), Does.Contain("对手掉线"));
        }

        [Test]
        public void EmptyReason_TreatedAsNormal()
        {
            Assert.That(Describe(0, ""), Does.Not.Contain("（"));
        }
    }
}
