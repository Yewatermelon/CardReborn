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
    /// MatchHost 命令上行校验测试（M13-T2）：信封级校验（载荷可解析 + 座位归属）
    /// 产出结构化拒绝（error + detail），非法命令状态零变更、宿主不抛异常。
    /// </summary>
    [TestFixture]
    public class MatchHostCommandValidationTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        private MatchController NewController()
        {
            return new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
        }

        /// <summary>挂载端点（显式座位）→ hello → 排空快照；返回权威状态序列化基线。</summary>
        private static string StartMatch(
            MatchController controller, MatchHost host,
            LoopbackEndpoint serverEp, LoopbackEndpoint clientEp, int seat)
        {
            host.Attach(serverEp, seat);
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);
            return MatchStateSerializer.Serialize(controller.State);
        }

        private static JsonValue SendAndGetStep(
            LoopbackEndpoint clientEp, MatchHost host, string raw)
        {
            clientEp.Send(raw);
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True, "应产出 step 消息");
            return JsonValue.Parse(stepRaw!);
        }

        private static JsonValue CommandEnvelope(string payloadJson)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(payloadJson));
        }

        private static void AssertRejected(
            JsonValue step, string expectedError, string detailFragment)
        {
            Assert.That(step.TryGetMember("accepted", out JsonValue accepted) && !accepted.BoolValue, Is.True);
            Assert.That(step.TryGetMember("error", out JsonValue error) && error.StringValue == expectedError, Is.True);
            Assert.That(step.TryGetMember("detail", out JsonValue detail) && detail.StringValue.Contains(detailFragment), Is.True);
        }

        // -----------------------------------------------------------------
        // AC-1: 座位归属校验——命令归属玩家与端点座位不一致 → 拒绝
        // -----------------------------------------------------------------

        [Test]
        public void SeatMismatch_CommandRejected_StateUnchanged()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            // 端点绑定非行动座位，却发送行动方座位的命令 → 归属不匹配（先手由种子决定，动态读取）
            int activeSeat = controller.State.ActivePlayerId;
            string before = StartMatch(controller, host, serverEp, clientEp, seat: 1 - activeSeat);

            JsonValue step = SendAndGetStep(clientEp, host, CommandEnvelope(
                CommandSerializer.Serialize(new EndTurnCommand(activeSeat))).ToJson());

            AssertRejected(step, "NotYourTurn", "座位");
            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(before));
        }

        // -----------------------------------------------------------------
        // AC-2: 座位匹配但非其回合 → RuleEngine 拒绝（detail 不含"座位"）
        // -----------------------------------------------------------------

        [Test]
        public void SeatMatched_NotPlayersTurn_RuleEngineRejects()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            string before = StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            JsonValue acceptedStep = SendAndGetStep(clientEp, host, CommandEnvelope(
                CommandSerializer.Serialize(new EndTurnCommand(activeSeat))).ToJson());
            Assert.That(acceptedStep.TryGetMember("accepted", out JsonValue ok) && ok.BoolValue, Is.True);
            Assert.That(host.StateVersion, Is.EqualTo(1));

            // 行动权已移交，同座位再次 EndTurn → 规则路径拒绝（detail 为回合说明而非座位说明）
            JsonValue rejectedStep = SendAndGetStep(clientEp, host, CommandEnvelope(
                CommandSerializer.Serialize(new EndTurnCommand(activeSeat))).ToJson());

            AssertRejected(rejectedStep, "NotYourTurn", "回合");
            Assert.That(rejectedStep.TryGetMember("detail", out JsonValue d) && !d.StringValue.Contains("座位"), Is.True);
            Assert.That(host.StateVersion, Is.EqualTo(1));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.Not.EqualTo(before));
        }

        // -----------------------------------------------------------------
        // AC-3: command 缺 payload → 拒绝 UnknownCommand，不抛异常
        // -----------------------------------------------------------------

        [Test]
        public void MissingPayload_ProducesRejectedStep_NoThrow()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            string before = StartMatch(controller, host, serverEp, clientEp, seat: 1);

            JsonValue step;
            Assert.DoesNotThrow(() =>
            {
                clientEp.Send("{\"kind\":\"command\"}");
                host.Pump();
                Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True);
                step = JsonValue.Parse(stepRaw!);
                AssertRejected(step, "UnknownCommand", "payload");
            });

            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(before));
        }

        // -----------------------------------------------------------------
        // AC-4a: payload 非法 JSON（字符串成员）→ 拒绝 UnknownCommand
        // -----------------------------------------------------------------

        [Test]
        public void MalformedPayloadJson_ProducesRejectedStep_NoThrow()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            string before = StartMatch(controller, host, serverEp, clientEp, seat: 1);

            JsonValue step = SendAndGetStep(clientEp, host,
                "{\"kind\":\"command\",\"payload\":\"not-a-command\"}");

            AssertRejected(step, "UnknownCommand", "载荷");
            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(before));
        }

        // -----------------------------------------------------------------
        // AC-4b: payload 未知命令类型标签 → 拒绝 UnknownCommand
        // -----------------------------------------------------------------

        [Test]
        public void UnknownTypeTag_ProducesRejectedStep_NoThrow()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            string before = StartMatch(controller, host, serverEp, clientEp, seat: 1);

            JsonValue step = SendAndGetStep(clientEp, host, CommandEnvelope(
                "{\"type\":\"Bogus\",\"playerId\":1}").ToJson());

            AssertRejected(step, "UnknownCommand", "类型");
            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(before));
        }

        // -----------------------------------------------------------------
        // AC-4c: payload 缺必需成员（EndTurn 无 playerId）→ 拒绝 UnknownCommand
        // -----------------------------------------------------------------

        [Test]
        public void PayloadMissingMember_ProducesRejectedStep_NoThrow()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            string before = StartMatch(controller, host, serverEp, clientEp, seat: 1);

            JsonValue step = SendAndGetStep(clientEp, host, CommandEnvelope(
                "{\"type\":\"EndTurn\"}").ToJson());

            AssertRejected(step, "UnknownCommand", "成员");
            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(before));
        }

        // -----------------------------------------------------------------
        // AC-5: 接受命令的 step 带 detail="" 且 error="None"
        // -----------------------------------------------------------------

        [Test]
        public void AcceptedStep_HasEmptyDetailAndNoneError()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            JsonValue step = SendAndGetStep(clientEp, host, CommandEnvelope(
                CommandSerializer.Serialize(new EndTurnCommand(activeSeat))).ToJson());

            Assert.That(step.TryGetMember("accepted", out JsonValue ok) && ok.BoolValue, Is.True);
            Assert.That(step.TryGetMember("error", out JsonValue error) && error.StringValue == "None", Is.True);
            Assert.That(step.TryGetMember("detail", out JsonValue detail) && detail.StringValue == string.Empty, Is.True);
        }
    }
}
