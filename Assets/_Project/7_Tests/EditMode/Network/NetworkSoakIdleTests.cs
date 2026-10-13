using System;
using System.Collections.Generic;
using System.Linq;
using Card.Domain.Config;
using Card.Network;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// M14-T7 挂机（Idle）探测：不提交任何玩家命令，验证心跳保活与空转稳定性。
    /// 大厅阶段 1200 ticks；对局中 60 000 ticks（≈100 分钟 @10 Hz，约 80 个回合超时窗口）。
    /// </summary>
    [TestFixture]
    public class NetworkSoakIdleTests
    {
        private CardDatabase _database = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
        }

        [Test]
        public void IdleProbe_LobbyPhase_ConnectionHolds()
        {
            using SoakStack stack = SoakStack.Connect(_database, seed: 9101);
            stack.PumpIdle(totalTicks: 1200, chunkTicks: 30);
            Assert.That(stack.Client.Connected, Is.True, "大厅空转后客户端不应断链。");
            Assert.That(stack.Session.IsConnected, Is.True, "大厅空转后房间会话不应心跳超时。");
            Assert.That(stack.MatchHost.StateVersion, Is.EqualTo(0UL), "未开局空转不应推进权威版本。");
        }

        [Test]
        public void IdleProbe_InMatch_HeartbeatHolds60kTicks()
        {
            using SoakStack stack = SoakStack.Connect(_database, seed: 9102);
            stack.StartMatch();
            try
            {
                stack.PumpIdle(totalTicks: 60000, chunkTicks: 30);
            }
            catch (Exception failure)
            {
                Assert.Fail("空转期间异常：" + failure.Message
                    + "，会话超时：" + stack.Session.TimedOut
                    + "，会话侧事件尾：" + DiagTail(stack.HostDiag)
                    + "，客户端侧事件尾：" + DiagTail(stack.Diag));
            }

            Assert.That(stack.Client.Connected, Is.True, "对局空转 60000 ticks 后客户端不应断链。"
                + "断链原因：" + (stack.Seat1.LastDisconnectReason ?? "-")
                + "，会话超时：" + stack.Session.TimedOut
                + "，会话侧事件尾：" + DiagTail(stack.HostDiag)
                + "，客户端侧事件尾：" + DiagTail(stack.Diag));
            Assert.That(stack.Seat1.DisconnectedCount, Is.EqualTo(0), "不应出现断链事件。");
            Assert.That(stack.MatchHost.StateVersion, Is.GreaterThan(0UL),
                "回合超时代发应推进版本（挂机非完全静止）。");
            Assert.That(stack.MatchHost.StateVersion, Is.LessThanOrEqualTo(80UL),
                "版本增长只应来自 750-tick 回合超时代发（60 000 / 750 = 80 上限）。");
            Assert.That(stack.Diag.Export().Any(e => e.EventType == NetworkEventType.PingReceived),
                Is.True, "应存在心跳 Ping 往返记录。");
        }

        [Test]
        public void IdleProbe_RepeatDispose_Idempotent()
        {
            SoakStack stack = SoakStack.Connect(_database, seed: 9103);
            stack.Dispose();
            Assert.DoesNotThrow(() => stack.Dispose(), "重复 Dispose 必须幂等无异常。");
        }

        /// <summary>诊断环形缓冲尾部摘要（失败信息用，看断链前最后几条事件）。</summary>
        private static string DiagTail(NetworkDiagnostics diag)
        {
            IEnumerable<string> parts = diag.Export()
                .Select(e => e.EventType + "@" + e.Tick + (string.IsNullOrEmpty(e.Detail) ? "" : "(" + e.Detail + ")"));
            return string.Join(",", parts);
        }
    }
}
