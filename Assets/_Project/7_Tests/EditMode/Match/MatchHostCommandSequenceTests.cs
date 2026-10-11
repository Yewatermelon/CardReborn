using System.Collections.Generic;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// MatchHost 命令序号与幂等测试（M13-T3）：按座位独立识别重复包——重发只执行
    /// 一次并幂等重放相同回执；过期/冲突序号结构化拒绝且状态零变更；step 回显 seq。
    /// </summary>
    [TestFixture]
    public class MatchHostCommandSequenceTests
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

        /// <summary>发送原文并取回 step 回执。</summary>
        private static JsonValue SendStep(LoopbackEndpoint clientEp, MatchHost host, string raw)
        {
            clientEp.Send(raw);
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True, "应产出 step 消息");
            return JsonValue.Parse(stepRaw!);
        }

        private static string CommandEnvelope(int? seq, string payloadJson)
        {
            JsonValue envelope = JsonValue.Object().Add("kind", JsonValue.From("command"));
            if (seq.HasValue)
            {
                envelope = envelope.Add("seq", JsonValue.From(seq.Value));
            }

            return envelope.Add("payload", JsonValue.Parse(payloadJson)).ToJson();
        }

        private static string EndTurnPayload(int seat)
        {
            return CommandSerializer.Serialize(new EndTurnCommand(seat));
        }

        private static string HeroPowerPayload(int seat)
        {
            return CommandSerializer.Serialize(new UseHeroPowerCommand(seat, TargetRef.None));
        }

        private static bool IsAccepted(JsonValue step)
            => step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue;

        private static int? ReadSeq(JsonValue step)
            => step.TryGetMember("seq", out JsonValue seq) && seq.Kind == JsonKind.Number
                ? seq.IntValue
                : (int?)null;

        private static void AssertRejected(JsonValue step, string expectedError, string detailFragment)
        {
            Assert.That(IsAccepted(step), Is.False, "应为拒绝回执");
            Assert.That(step.TryGetMember("error", out JsonValue error) && error.StringValue == expectedError, Is.True);
            Assert.That(step.TryGetMember("detail", out JsonValue detail)
                && detail.StringValue.Contains(detailFragment), Is.True);
        }

        [Test]
        public void DuplicateSeq_AcceptedCommand_ReplaysSameStep_ExecutesOnce()
        {
            // AC-1：接受命令原样重发 → 相同回执（accepted/seq=1/version=1），只执行一次
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            string raw = CommandEnvelope(1, EndTurnPayload(activeSeat));
            Assert.That(IsAccepted(SendStep(clientEp, host, raw)), Is.True, "首次发送应被接受");
            Assert.That(host.StateVersion, Is.EqualTo(1));
            string afterOnce = MatchStateSerializer.Serialize(controller.State);

            JsonValue replay = SendStep(clientEp, host, raw);
            Assert.That(IsAccepted(replay), Is.True, "重发应幂等重放接受回执");
            Assert.That(ReadSeq(replay), Is.EqualTo(1));
            Assert.That(replay.TryGetMember("version", out JsonValue version) && version.IntValue == 1, Is.True);
            Assert.That(host.StateVersion, Is.EqualTo(1), "重发不得再次推进版本");
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(afterOnce), "重发不得再次改变状态");
        }

        [Test]
        public void DuplicateSeq_RejectedCommand_ReplaysRejectedStep()
        {
            // AC-2：被拒命令原样重发 → 相同拒绝回执（拒绝结果也可幂等重放）
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            Assert.That(IsAccepted(SendStep(clientEp, host,
                CommandEnvelope(1, EndTurnPayload(activeSeat)))), Is.True);

            string rejectedRaw = CommandEnvelope(2, EndTurnPayload(activeSeat));
            AssertRejected(SendStep(clientEp, host, rejectedRaw), "NotYourTurn", "回合");
            string stateAfterReject = MatchStateSerializer.Serialize(controller.State);

            JsonValue replay = SendStep(clientEp, host, rejectedRaw);
            AssertRejected(replay, "NotYourTurn", "回合");
            Assert.That(ReadSeq(replay), Is.EqualTo(2));
            Assert.That(host.StateVersion, Is.EqualTo(1));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(stateAfterReject));
        }

        [Test]
        public void StaleSeq_LowerThanConsumed_Rejected_StateUnchanged()
        {
            // AC-3：低序号（seq=2 已消费后发 seq=1 新载荷）→ StaleSequence，状态零变更
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            Assert.That(IsAccepted(SendStep(clientEp, host,
                CommandEnvelope(2, EndTurnPayload(activeSeat)))), Is.True);
            string afterSeq2 = MatchStateSerializer.Serialize(controller.State);

            JsonValue stale = SendStep(clientEp, host, CommandEnvelope(1, HeroPowerPayload(activeSeat)));
            AssertRejected(stale, "StaleSequence", "过期");
            Assert.That(host.StateVersion, Is.EqualTo(1));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(afterSeq2));
        }

        [Test]
        public void SameSeq_DifferentPayload_Rejected_StateUnchanged()
        {
            // AC-4：同 seq 不同载荷 → StaleSequence（序号已被占用），状态零变更
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            Assert.That(IsAccepted(SendStep(clientEp, host,
                CommandEnvelope(1, EndTurnPayload(activeSeat)))), Is.True);
            string afterSeq1 = MatchStateSerializer.Serialize(controller.State);

            JsonValue conflict = SendStep(clientEp, host, CommandEnvelope(1, HeroPowerPayload(activeSeat)));
            AssertRejected(conflict, "StaleSequence", "已被其他命令使用");
            Assert.That(host.StateVersion, Is.EqualTo(1));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(afterSeq1));
        }

        [Test]
        public void SkippedSeq_Accepted_ThenLowerSeqStale()
        {
            // AC-5：跳号允许（首条 seq=5 直接执行），此后低序号过期
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            JsonValue accepted = SendStep(clientEp, host, CommandEnvelope(5, EndTurnPayload(activeSeat)));
            Assert.That(IsAccepted(accepted), Is.True, "跳号命令应直接执行");
            Assert.That(ReadSeq(accepted), Is.EqualTo(5));
            Assert.That(host.StateVersion, Is.EqualTo(1));

            JsonValue stale = SendStep(clientEp, host, CommandEnvelope(3, HeroPowerPayload(activeSeat)));
            AssertRejected(stale, "StaleSequence", "过期");
            Assert.That(host.StateVersion, Is.EqualTo(1));
        }

        [Test]
        public void StepEchoesSeq_OnlyWhenEnvelopeHasSeq()
        {
            // AC-6：带 seq 命令回执回显 seq；无 seq 命令回执不含 seq 字段（行为回归）
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            JsonValue withSeq = SendStep(clientEp, host, CommandEnvelope(7, EndTurnPayload(activeSeat)));
            Assert.That(ReadSeq(withSeq), Is.EqualTo(7), "带 seq 命令的回执应回显 seq");

            JsonValue withoutSeq = SendStep(clientEp, host, CommandEnvelope(null, EndTurnPayload(activeSeat)));
            Assert.That(withoutSeq.TryGetMember("seq", out _), Is.False, "无 seq 命令的回执不应含 seq 字段");
            Assert.That(IsAccepted(withoutSeq), Is.False, "无序号模式行为不变（此处非其回合被拒）");
        }

        [Test]
        public void InvalidSeq_NegativeOrNonNumber_UnknownCommand()
        {
            // AC-7：seq 字段非法（负数 / 非数字）→ UnknownCommand，状态零变更
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            string baseline = StartMatch(controller, host, serverEp, clientEp, seat: activeSeat);

            AssertRejected(SendStep(clientEp, host, CommandEnvelope(-1, EndTurnPayload(activeSeat))),
                "UnknownCommand", "seq");
            AssertRejected(SendStep(clientEp, host,
                "{\"kind\":\"command\",\"seq\":\"1\",\"payload\":" + EndTurnPayload(activeSeat) + "}"),
                "UnknownCommand", "seq");

            Assert.That(host.StateVersion, Is.EqualTo(0));
            Assert.That(MatchStateSerializer.Serialize(controller.State), Is.EqualTo(baseline));
        }

        [Test]
        public void SequencesIndependentPerSeat()
        {
            // AC-8：双座位序号独立——座位 A 消费 seq=1 后座位 B 的 seq=1 仍可执行
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            c0.Send("{\"kind\":\"hello\"}");
            c1.Send("{\"kind\":\"hello\"}");
            host.Pump();
            c0.TryReceive(out _);
            c1.TryReceive(out _);

            int firstSeat = controller.State.ActivePlayerId;
            LoopbackEndpoint firstClient = firstSeat == 0 ? c0 : c1;
            LoopbackEndpoint secondClient = firstSeat == 0 ? c1 : c0;
            Assert.That(IsAccepted(SendStep(firstClient, host,
                CommandEnvelope(1, EndTurnPayload(firstSeat)))), Is.True, "座位 A 的 seq=1 应执行");

            int secondSeat = 1 - firstSeat;
            JsonValue second = SendStep(secondClient, host, CommandEnvelope(1, EndTurnPayload(secondSeat)));
            Assert.That(IsAccepted(second), Is.True, "座位 B 的 seq=1 不受座位 A 序号影响");
            Assert.That(ReadSeq(second), Is.EqualTo(1));
            Assert.That(host.StateVersion, Is.EqualTo(2));
        }

        [Test]
        public void SeatRejection_DoesNotConsumeSeq()
        {
            // AC-9：座位归属拒绝不消费 seq——同 seq 的合法命令仍按可执行进入结算
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int activeSeat = controller.State.ActivePlayerId;
            int ownSeat = 1 - activeSeat;
            StartMatch(controller, host, serverEp, clientEp, seat: ownSeat);

            // 冒用行动方 PlayerId → 座位归属拒绝（T2 路径，先于序号校验）
            AssertRejected(SendStep(clientEp, host, CommandEnvelope(1, EndTurnPayload(activeSeat))),
                "NotYourTurn", "座位");

            // 同 seq=1 发自己座位命令：若 seq 被错误消费会走幂等/冲突分支而非进入结算
            JsonValue second = SendStep(clientEp, host, CommandEnvelope(1, EndTurnPayload(ownSeat)));
            AssertRejected(second, "NotYourTurn", "回合");
            Assert.That(second.TryGetMember("detail", out JsonValue d) && !d.StringValue.Contains("座位"), Is.True,
                "应进入规则结算路径（回合拒绝）而非序号/座位分支");
            Assert.That(host.StateVersion, Is.EqualTo(0));
        }
    }
}
