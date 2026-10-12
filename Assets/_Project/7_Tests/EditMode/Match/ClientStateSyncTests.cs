using System.Collections.Generic;
using System.Linq;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Match;
using NUnit.Framework;
// 夹具助手（对局/信封/步收取/座位副本）来自 StateSyncTestHarness（M13-T6 拆出）
using static Card.Tests.EditMode.Match.StateSyncTestHarness;

namespace Card.Tests.EditMode.Match
{
    /// <summary>客户端状态同步器测试（M13-T4）：StateDelta 版本裁决——重复丢弃、空洞不猜合并、拒绝步不动状态。</summary>
    [TestFixture]
    public class ClientStateSyncTests
    {
        [Test]
        public void Snapshot_CarriesVersion_AndLoadsBaseline()
        {
            // AC-1：快照含 version 字段；载入后基线与座位投影权威一致（M13-T6 裁剪）
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            int seat = controller.State.ActivePlayerId;
            host.Attach(serverEp, seat);
            JsonValue snapshot = JsonValue.Parse(HelloSnapshot(host, clientEp));
            Assert.That(snapshot.TryGetMember("version", out JsonValue v) && v.IntValue == 0, Is.True,
                "快照应携带 version=0");

            ClientStateSync sync = new ClientStateSync();
            sync.ApplySnapshot(snapshot);
            Assert.That(sync.LocalVersion, Is.EqualTo(0UL));
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, seat))));
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
        public void TwoSeats_MultipleCommands_ClientViewMatchesProjectedAuthority()
        {
            // AC-3（核心）：双座位交替命令，客户端应用每步增量后与各自座位的投影权威一致（M13-T6）
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
                bool activeIs0 = active == 0;
                JsonValue[] steps = BroadcastSteps(activeIs0 ? c0 : c1, activeIs0 ? c1 : c0, host,
                    CommandEnvelope(seq, EndTurnPayload(active)));
                Assert.That(IsAccepted(steps[0]), Is.True, $"第 {round + 1} 步应被接受");
                // 各 sync 只应用自己端点收到的副本（M13-T6 起步按座位裁剪，他人视图不可混用）
                Assert.That(sync0.ApplyStep(StepForSeat(0, active, steps)).Status,
                    Is.EqualTo(StepApplyStatus.Applied));
                Assert.That(sync1.ApplyStep(StepForSeat(1, active, steps)).Status,
                    Is.EqualTo(StepApplyStatus.Applied));
            }

            Assert.That(MatchStateSerializer.Serialize(sync0.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, 0))),
                "座位 0 视图应与其投影权威一致");
            Assert.That(MatchStateSerializer.Serialize(sync1.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, 1))),
                "座位 1 视图应与其投影权威一致");
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
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, seat))));
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
            // sync 只应用 c0（座位 0）端点的副本（M13-T6 起步按座位裁剪）
            JsonValue step1 = StepForSeat(0, first, BroadcastSteps(
                Ep(first), Ep(second), host, CommandEnvelope(1, EndTurnPayload(first))));
            JsonValue step2 = StepForSeat(0, second, BroadcastSteps(
                Ep(second), Ep(first), host, CommandEnvelope(1, EndTurnPayload(second))));
            JsonValue step3 = StepForSeat(0, first, BroadcastSteps(
                Ep(first), Ep(second), host, CommandEnvelope(2, EndTurnPayload(first))));
            Assert.That(MemberNumber(step3, "basedOn"), Is.EqualTo(2UL));

            Assert.That(sync.ApplyStep(step1).Status, Is.EqualTo(StepApplyStatus.Applied));
            string afterStep1 = MatchStateSerializer.Serialize(sync.State);

            StepOutcome gap = sync.ApplyStep(step3);
            Assert.That(gap.Status, Is.EqualTo(StepApplyStatus.VersionGap), "跳过 step2 应判版本空洞");
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(afterStep1), "空洞步不得猜着合并");
            Assert.That(sync.LocalVersion, Is.EqualTo(1UL));

            Assert.That(sync.ApplyStep(step2).Status, Is.EqualTo(StepApplyStatus.Applied));
            Assert.That(sync.ApplyStep(step3).Status, Is.EqualTo(StepApplyStatus.Applied), "补齐后 step3 可重新应用");
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, 0))),
                "补齐后应恢复与座位投影权威一致");
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

            // 下一条增量（basedOn=1）正常应用不判空洞（取 c1 端点的副本）
            int second = controller.State.ActivePlayerId;
            JsonValue[] next = BroadcastSteps(second == 0 ? c0 : c1, second == 0 ? c1 : c0, host,
                CommandEnvelope(1, EndTurnPayload(second)));
            StepOutcome outcome = lateSync.ApplyStep(StepForSeat(1, second, next));
            Assert.That(outcome.Status, Is.EqualTo(StepApplyStatus.Applied), "基于快照版本的增量不应判空洞");
            Assert.That(MatchStateSerializer.Serialize(lateSync.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(controller.State, 1))));
        }

        [Test]
        public void HasSnapshot_FalseBeforeSnapshot_TrueAfter()
        {
            ClientStateSync sync = new ClientStateSync();
            Assert.That(sync.HasSnapshot, Is.False, "未载入快照时视图不可用");

            sync.ApplySnapshot(JsonValue.Parse(
                "{\"version\":0,\"state\":" + MatchStateSerializer.Serialize(NewController().State) + "}"));

            Assert.That(sync.HasSnapshot, Is.True);
        }
    }
}
