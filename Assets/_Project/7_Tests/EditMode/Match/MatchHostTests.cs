using System;
using System.Collections.Generic;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// MatchHost 权威运行时核心测试（M11-T2）：提炼自 M4-T10 LoopbackMatchTests。
    /// 验证 MatchHost 在无 Unity 环境下能跑完整对局 + 事件/增量一致性。
    /// </summary>
    [TestFixture]
    public class MatchHostTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        // -----------------------------------------------------------------
        // AC-1: Hello → 快照（含 NextInstanceId，FormatVersion=2）
        // -----------------------------------------------------------------

        [Test]
        public void HelloSnapshot_ViewMatchesAuthority()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();

            Assert.That(clientEp.TryReceive(out string? raw), Is.True);
            JsonValue json = JsonValue.Parse(raw!);
            Assert.That(json.TryGetMember("kind", out JsonValue kind) && kind.StringValue == "snapshot", Is.True);
            Assert.That(host.StateVersion, Is.EqualTo(0));

            Assert.That(json.TryGetMember("state", out JsonValue stateJson), Is.True);
            MatchState? restored = MatchStateSerializer.ReadStateTree(stateJson);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.NextInstanceId, Is.EqualTo(controller.State.NextInstanceId));
            // M13-T6：快照按座位裁剪——座位 0 视图 = 投影后的权威（自己手牌完整、双方牌库遮蔽）
            Assert.That(MatchStateSerializer.Serialize(restored), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, 0))));
        }

        // -----------------------------------------------------------------
        // AC-2: EndTurn 接受 → StateVersion+1 + Changes 非空 + Events 非空
        // -----------------------------------------------------------------

        [Test]
        public void EndTurn_Accepted_StateVersionIncrementsWithChangesAndEvents()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            // M13-T2 座位归属校验：端点须挂实际行动座位（seed 42 先手非固定座位 0）
            host.Attach(serverEp, controller.State.ActivePlayerId);

            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            SendCommand(clientEp, new EndTurnCommand(controller.State.ActivePlayerId));
            host.Pump();

            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True);
            JsonValue stepJson = JsonValue.Parse(stepRaw!);
            stepJson.TryGetMember("accepted", out JsonValue accepted);
            Assert.That(accepted.BoolValue, Is.True);
            Assert.That(host.StateVersion, Is.EqualTo(1));

            Assert.That(stepJson.TryGetMember("changes", out JsonValue changes) && changes.Items.Count > 0, Is.True);
            Assert.That(stepJson.TryGetMember("events", out JsonValue events) && events.Items.Count > 0, Is.True);
        }

        // -----------------------------------------------------------------
        // AC-3: 非行动方命令被拒绝 → StateVersion 不变
        // -----------------------------------------------------------------

        [Test]
        public void NonActivePlayer_CommandRejected_StateVersionUnchanged()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            int nonActiveSeat = 1 - controller.State.ActivePlayerId;
            SendCommand(clientEp, new EndTurnCommand(nonActiveSeat));
            host.Pump();

            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True);
            JsonValue stepJson = JsonValue.Parse(stepRaw!);
            stepJson.TryGetMember("accepted", out JsonValue accepted);
            Assert.That(accepted.BoolValue, Is.False);
            stepJson.TryGetMember("error", out JsonValue error);
            Assert.That(error.StringValue, Is.EqualTo("NotYourTurn"));
        }

        // -----------------------------------------------------------------
        // AC-4: 双客户端疲劳脚本完整对局
        // -----------------------------------------------------------------

        [Test]
        public void DualClient_FatigueScript_FinishesWithConsistentViews()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            // M13-T2 座位归属校验：每个端点绑定一个座位，按行动方发送命令
            host.Attach(s0, 0);
            host.Attach(s1, 1);

            c0.Send("{\"kind\":\"hello\"}");
            c1.Send("{\"kind\":\"hello\"}");
            host.Pump();
            c0.TryReceive(out _);
            c1.TryReceive(out _);

            int initialActive = controller.State.ActivePlayerId;
            int acceptedCount = 0;

            for (int step = 0; step < MatchControllerFixtures.MaxScriptSteps && !controller.IsFinished; step++)
            {
                int seat = controller.State.ActivePlayerId;
                LoopbackEndpoint actor = seat == 0 ? c0 : c1;
                SendCommand(actor, new EndTurnCommand(seat));
                host.Pump();
                c0.TryReceive(out _);
                c1.TryReceive(out _);
                acceptedCount++;
            }

            Assert.That(controller.IsFinished, Is.True);
            Assert.That(controller.LastOutcome, Is.Not.Null);
            Assert.That(controller.LastOutcome!.WinnerId, Is.EqualTo(initialActive));
            Assert.That(host.StateVersion, Is.EqualTo((ulong)acceptedCount));
        }

        // -----------------------------------------------------------------
        // M13-T7 AC-3: Detach——摘除端点后广播不再送达（重连清理）
        // -----------------------------------------------------------------

        [Test]
        public void Detach_RemovedEndpoint_StopsReceivingBroadcasts()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            c0.Send("{\"kind\":\"hello\"}");
            c1.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(c0.TryReceive(out _), Is.True);
            Assert.That(c1.TryReceive(out _), Is.True);

            // 模拟重连：座位 0 旧端点摘除，新端点挂入
            host.Detach(s0);
            Assert.That(host.AttachedSeats, Is.EqualTo(new[] { 1 }), "座位映射应同步移除");
            LoopbackLink.CreatePair(out LoopbackEndpoint s0New, out LoopbackEndpoint c0New);
            host.Attach(s0New, 0);
            c0New.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(c0New.TryReceive(out _), Is.True, "新端点应收到快照");

            void EndTurnFrom(int seat, LoopbackEndpoint actorEp)
            {
                SendCommand(actorEp, new EndTurnCommand(seat));
                host.Pump();
                actorEp.TryReceive(out _);
                c1.TryReceive(out _);
                c0New.TryReceive(out _);
                Assert.That(c0.TryReceive(out _), Is.False, "已摘除端点不应再收到任何广播");
            }

            int first = controller.State.ActivePlayerId;
            EndTurnFrom(first, first == 0 ? c0New : c1);
            EndTurnFrom(1 - first, (1 - first) == 0 ? c0New : c1);
        }

        // -----------------------------------------------------------------
        // M14-T3-B5: 重连接管重置座位序号基线（Detach 移除收件箱）——
        // 否则重连客户端（新进程，seq 从 1 重新计数）所有命令都被 StaleSequence 拒绝
        // -----------------------------------------------------------------

        [Test]
        public void Detach_RejoinSeat_SequenceBaselineResets()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            c0.Send("{\"kind\":\"hello\"}");
            c1.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Drain(c0, c1);

            int first = controller.State.ActivePlayerId;
            int second = 1 - first;

            // 断线前：座位 second 发过一条被规则拒绝的命令（序号仍被消费，LastSeq=1）
            SendSequencedCommand(second == 0 ? c0 : c1, new EndTurnCommand(second), 1);
            host.Pump();
            Drain(c0, c1);
            Assert.That(controller.State.ActivePlayerId, Is.EqualTo(first), "非行动方命令应被规则拒绝");

            // 行动方正常流转一轮 → 轮到 second
            SendCommand(first == 0 ? c0 : c1, new EndTurnCommand(first));
            host.Pump();
            Drain(c0, c1);
            Assert.That(controller.State.ActivePlayerId, Is.EqualTo(second));

            // 模拟重连：座位 second 换新端点（客户端进程重启后 seq 从 1 重新计数）
            host.Detach(second == 0 ? s0 : s1);
            LoopbackLink.CreatePair(out LoopbackEndpoint sNew, out LoopbackEndpoint cNew);
            host.Attach(sNew, second);
            cNew.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(cNew.TryReceive(out _), Is.True, "重连端点应收到快照");

            // 修复目标：重连后同座位 seq=1 重新可执行（修复前 StaleSequence 拒绝导致对局卡死）
            SendSequencedCommand(cNew, new EndTurnCommand(second), 1);
            host.Pump();
            Drain(c0, c1, cNew);
            Assert.That(controller.State.ActivePlayerId, Is.EqualTo(first), "重连后同座位 seq=1 应重新可执行");
        }

        // -----------------------------------------------------------------
        // 辅助
        // -----------------------------------------------------------------

        private MatchController NewController()
        {
            return new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42),
                _database);
        }

        private static void SendCommand(LoopbackEndpoint endpoint, IGameCommand command)
        {
            string payloadJson = CommandSerializer.Serialize(command);
            JsonValue payload = JsonValue.Parse(payloadJson);
            JsonValue msg = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", payload);
            endpoint.Send(msg.ToJson());
        }

        private static void SendSequencedCommand(LoopbackEndpoint endpoint, IGameCommand command, int seq)
        {
            JsonValue payload = JsonValue.Parse(CommandSerializer.Serialize(command));
            JsonValue msg = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", payload)
                .Add("seq", JsonValue.From(seq));
            endpoint.Send(msg.ToJson());
        }

        private static void Drain(params LoopbackEndpoint[] endpoints)
        {
            for (int i = 0; i < endpoints.Length; i++)
            {
                while (endpoints[i].TryReceive(out _))
                {
                }
            }
        }
    }
}
