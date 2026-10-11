using System.Collections.Generic;
using System.Linq;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>客户端状态同步器测试（M13-T4）：StateDelta 版本裁决——重复丢弃、空洞不猜合并、拒绝步不动状态。</summary>
    [TestFixture]
    public class ClientStateSyncTests
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
            => new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);

        private static string CommandEnvelope(int seq, string payloadJson)
            => JsonValue.Object()
                    .Add("kind", JsonValue.From("command"))
                    .Add("seq", JsonValue.From(seq))
                    .Add("payload", JsonValue.Parse(payloadJson))
                    .ToJson();

        private static string EndTurnPayload(int seat)
            => CommandSerializer.Serialize(new EndTurnCommand(seat));

        private static string HelloSnapshot(MatchHost host, LoopbackEndpoint clientEp)
        {
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? snapRaw), Is.True, "hello 应回快照");
            return snapRaw!;
        }

        private static ClientStateSync NewSync(MatchHost host, LoopbackEndpoint clientEp)
        {
            ClientStateSync sync = new ClientStateSync();
            sync.ApplySnapshot(JsonValue.Parse(HelloSnapshot(host, clientEp)));
            return sync;
        }

        private static JsonValue SendStep(LoopbackEndpoint clientEp, MatchHost host, string raw)
        {
            clientEp.Send(raw);
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True, "应产出 step 消息");
            return JsonValue.Parse(stepRaw!);
        }

        /// <summary>双端点广播取回：Pump 后两个客户端端点各有一份相同 step，逐一取出。</summary>
        private static JsonValue[] BroadcastSteps(
            LoopbackEndpoint senderEp, LoopbackEndpoint otherEp, MatchHost host, string raw)
        {
            senderEp.Send(raw);
            host.Pump();
            bool toSender = senderEp.TryReceive(out string? s);
            bool toOther = otherEp.TryReceive(out string? o);
            Assert.That(toSender && toOther, Is.True, "广播步应到达两端点");
            return new[] { JsonValue.Parse(s!), JsonValue.Parse(o!) };
        }

        private static bool IsAccepted(JsonValue step)
            => step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue;

        private static ulong MemberNumber(JsonValue step, string key)
        {
            Assert.That(step.TryGetMember(key, out JsonValue value) && value.Kind == JsonKind.Number,
                Is.True, $"step 应含数字字段 {key}");
            return (ulong)value.IntValue;
        }

        private static int EventCount(JsonValue step)
            => step.TryGetMember("events", out JsonValue events) ? events.Items.Count : 0;

        [Test]
        public void Snapshot_CarriesVersion_AndLoadsBaseline()
        {
            // AC-1：快照含 version 字段；载入后基线与权威一致
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp, controller.State.ActivePlayerId);
            JsonValue snapshot = JsonValue.Parse(HelloSnapshot(host, clientEp));
            Assert.That(snapshot.TryGetMember("version", out JsonValue v) && v.IntValue == 0, Is.True,
                "快照应携带 version=0");

            ClientStateSync sync = new ClientStateSync();
            sync.ApplySnapshot(snapshot);
            Assert.That(sync.LocalVersion, Is.EqualTo(0UL));
            Assert.That(MatchStateSerializer.Serialize(sync.State),
                Is.EqualTo(MatchStateSerializer.Serialize(controller.State)));
        }

        [Test]
        public void AcceptedCommand_StepCarriesBasedOn_AndApplies()
        {
            // AC-2：首条接受命令 step 含 version=1 / basedOn=0 / changes 非空；应用后版本推进
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int seat = controller.State.ActivePlayerId;
            host.Attach(serverEp, seat);
            ClientStateSync sync = NewSync(host, clientEp);
            JsonValue step = SendStep(clientEp, host, CommandEnvelope(1, EndTurnPayload(seat)));
            Assert.That(IsAccepted(step), Is.True);
            Assert.That(MemberNumber(step, "version"), Is.EqualTo(1UL));
            Assert.That(MemberNumber(step, "basedOn"), Is.EqualTo(0UL));
            Assert.That(step.TryGetMember("changes", out JsonValue changes) && changes.Items.Count > 0,
                Is.True, "接受命令应产生状态变化");

            StepOutcome outcome = sync.ApplyStep(step);
            Assert.That(outcome.Status, Is.EqualTo(StepApplyStatus.Applied));
            Assert.That(sync.LocalVersion, Is.EqualTo(1UL));
        }

        [Test]
        public void TwoSeats_MultipleCommands_ClientViewMatchesAuthority()
        {
            // AC-3（核心）：双座位交替命令，客户端应用每步增量后与权威全文一致
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            ClientStateSync sync0 = NewSync(host, c0);
            ClientStateSync sync1 = NewSync(host, c1);
            int seq0 = 1, seq1 = 1;
            for (int round = 0; round < 3; round++)
            {
                int active = controller.State.ActivePlayerId;
                int seq = active == 0 ? seq0++ : seq1++;
                JsonValue[] steps = BroadcastSteps(active == 0 ? c0 : c1, active == 0 ? c1 : c0, host,
                    CommandEnvelope(seq, EndTurnPayload(active)));
                Assert.That(IsAccepted(steps[0]), Is.True, $"第 {round + 1} 步应被接受");
                Assert.That(sync0.ApplyStep(steps[0]).Status, Is.EqualTo(StepApplyStatus.Applied));
                Assert.That(sync1.ApplyStep(steps[1]).Status, Is.EqualTo(StepApplyStatus.Applied));
            }

            string authority = MatchStateSerializer.Serialize(controller.State);
            Assert.That(MatchStateSerializer.Serialize(sync0.State), Is.EqualTo(authority), "座位 0 视图应与权威一致");
            Assert.That(MatchStateSerializer.Serialize(sync1.State), Is.EqualTo(authority), "座位 1 视图应与权威一致");
            Assert.That(host.StateVersion, Is.EqualTo(3UL));
            Assert.That(sync0.LocalVersion, Is.EqualTo(host.StateVersion));
        }

        [Test]
        public void RejectedStep_NoStateChange_ErrorTransmitted()
        {
            // AC-4：拒绝步不动状态与版本，Error/Detail 透传
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int seat = controller.State.ActivePlayerId;
            host.Attach(serverEp, seat);
            ClientStateSync sync = NewSync(host, clientEp);
            JsonValue accepted = SendStep(clientEp, host, CommandEnvelope(1, EndTurnPayload(seat)));
            Assert.That(IsAccepted(accepted), Is.True, "首条命令应被接受（制造非本回合局面）");
            Assert.That(sync.ApplyStep(accepted).Status, Is.EqualTo(StepApplyStatus.Applied));

            JsonValue rejected = SendStep(clientEp, host, CommandEnvelope(2, EndTurnPayload(seat)));
            Assert.That(IsAccepted(rejected), Is.False);
            Assert.That(MemberNumber(rejected, "basedOn"), Is.EqualTo(MemberNumber(rejected, "version")),
                "拒绝步不推进版本，basedOn 应等于 version");

            StepOutcome outcome = sync.ApplyStep(rejected);
            Assert.That(outcome.Status, Is.EqualTo(StepApplyStatus.Rejected));
            Assert.That(outcome.Accepted, Is.False);
            Assert.That(outcome.Error, Is.EqualTo(CommandError.NotYourTurn));
            Assert.That(outcome.Detail, Does.Contain("回合"));
            Assert.That(sync.LocalVersion, Is.EqualTo(1UL), "拒绝步不得推进客户端版本");
            Assert.That(MatchStateSerializer.Serialize(sync.State),
                Is.EqualTo(MatchStateSerializer.Serialize(controller.State)));
        }

        [Test]
        public void DuplicateStep_Discarded()
        {
            // AC-5：同一 accepted step 二次应用（T3 幂等重放场景）→ 丢弃，状态与版本不变
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int seat = controller.State.ActivePlayerId;
            host.Attach(serverEp, seat);
            ClientStateSync sync = NewSync(host, clientEp);
            JsonValue step = SendStep(clientEp, host, CommandEnvelope(1, EndTurnPayload(seat)));
            Assert.That(sync.ApplyStep(step).Status, Is.EqualTo(StepApplyStatus.Applied));
            string afterOnce = MatchStateSerializer.Serialize(sync.State);

            StepOutcome replay = sync.ApplyStep(step);
            Assert.That(replay.Status, Is.EqualTo(StepApplyStatus.DuplicateDiscarded));
            Assert.That(sync.LocalVersion, Is.EqualTo(1UL));
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(afterOnce));
        }

        [Test]
        public void VersionGap_Detected_AndRecoversByReplay()
        {
            // AC-6：跳过中间步 → VersionGap 不猜合并；按序补齐后恢复与权威一致
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            int first = controller.State.ActivePlayerId;
            int second = 1 - first;
            ClientStateSync sync = NewSync(host, c0);
            LoopbackEndpoint Ep(int seat) => seat == 0 ? c0 : c1;
            JsonValue step1 = BroadcastSteps(Ep(first), Ep(second), host, CommandEnvelope(1, EndTurnPayload(first)))[0];
            JsonValue step2 = BroadcastSteps(Ep(second), Ep(first), host, CommandEnvelope(1, EndTurnPayload(second)))[0];
            JsonValue step3 = BroadcastSteps(Ep(first), Ep(second), host, CommandEnvelope(2, EndTurnPayload(first)))[0];
            Assert.That(MemberNumber(step3, "basedOn"), Is.EqualTo(2UL));

            Assert.That(sync.ApplyStep(step1).Status, Is.EqualTo(StepApplyStatus.Applied));
            string afterStep1 = MatchStateSerializer.Serialize(sync.State);

            StepOutcome gap = sync.ApplyStep(step3);
            Assert.That(gap.Status, Is.EqualTo(StepApplyStatus.VersionGap), "跳过 step2 应判版本空洞");
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(afterStep1), "空洞步不得猜着合并");
            Assert.That(sync.LocalVersion, Is.EqualTo(1UL));

            Assert.That(sync.ApplyStep(step2).Status, Is.EqualTo(StepApplyStatus.Applied));
            Assert.That(sync.ApplyStep(step3).Status, Is.EqualTo(StepApplyStatus.Applied), "补齐后 step3 可重新应用");
            Assert.That(MatchStateSerializer.Serialize(sync.State),
                Is.EqualTo(MatchStateSerializer.Serialize(controller.State)), "补齐后应恢复与权威一致");
        }

        [Test]
        public void EventsPassedThrough()
        {
            // AC-7：Applied 步透传事件原文（条数一致、内容含 TurnEnded），供表现层播动画
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int seat = controller.State.ActivePlayerId;
            host.Attach(serverEp, seat);
            ClientStateSync sync = NewSync(host, clientEp);
            JsonValue step = SendStep(clientEp, host, CommandEnvelope(1, EndTurnPayload(seat)));
            Assert.That(EventCount(step), Is.GreaterThan(0), "结束回合应产生事件");

            StepOutcome outcome = sync.ApplyStep(step);
            Assert.That(outcome.Events.Count, Is.EqualTo(EventCount(step)));
            Assert.That(outcome.Events.Any(e => e.TryGetMember("type", out JsonValue t)
                && t.StringValue == "TurnEnded"), Is.True, "应透传 TurnEnded 事件");
        }

        [Test]
        public void LateJoinSnapshotBaseline_NoGap()
        {
            // AC-8：权威推进后加入的端点——快照携带 version=1 基线，后续增量不判空洞
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            int first = controller.State.ActivePlayerId;

            // 先手执行一条命令 → 权威版本 1（回执从先手端点确认）；随后排空两端点广播步
            LoopbackEndpoint firstClient = first == 0 ? c0 : c1;
            firstClient.Send(CommandEnvelope(1, EndTurnPayload(first)));
            host.Pump();
            Assert.That(firstClient.TryReceive(out string? step1Raw)
                && IsAccepted(JsonValue.Parse(step1Raw!)), Is.True, "先手命令应被接受");
            while (c0.TryReceive(out _)) { }
            while (c1.TryReceive(out _)) { }

            JsonValue snapshot = JsonValue.Parse(HelloSnapshot(host, c1));
            Assert.That(snapshot.TryGetMember("version", out JsonValue v) && v.IntValue == 1, Is.True,
                "后加入端点快照应携带当前权威版本");
            ClientStateSync lateSync = new ClientStateSync();
            lateSync.ApplySnapshot(snapshot);
            Assert.That(lateSync.LocalVersion, Is.EqualTo(1UL));

            // 下一条增量（basedOn=1）正常应用不判空洞（取 c1 端点收到的副本）
            int second = controller.State.ActivePlayerId;
            JsonValue[] next = BroadcastSteps(second == 0 ? c0 : c1, second == 0 ? c1 : c0, host,
                CommandEnvelope(1, EndTurnPayload(second)));
            StepOutcome outcome = lateSync.ApplyStep(next[second == 0 ? 1 : 0]);
            Assert.That(outcome.Status, Is.EqualTo(StepApplyStatus.Applied), "基于快照版本的增量不应判空洞");
            Assert.That(MatchStateSerializer.Serialize(lateSync.State),
                Is.EqualTo(MatchStateSerializer.Serialize(controller.State)));
        }
    }
}
