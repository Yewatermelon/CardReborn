using System.Collections.Generic;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>快照回退测试（M13-T5）：SnapshotRequest → 全量快照单播；版本冲突后恢复一致，过期包丢弃。</summary>
    [TestFixture]
    public class SnapshotFallbackTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;
        private MatchController _controller = null!;
        private MatchHost _host = null!;
        private LoopbackEndpoint _s0 = null!, _s1 = null!, _c0 = null!, _c1 = null!;
        private ClientStateSync _sync = null!;

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

        private static ClientStateSync NewSync(MatchHost host, LoopbackEndpoint clientEp)
        {
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? snapRaw), Is.True, "hello 应回快照");
            ClientStateSync sync = new ClientStateSync();
            sync.ApplySnapshot(JsonValue.Parse(snapRaw!));
            return sync;
        }

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

        private static ulong MemberNumber(JsonValue step, string key)
        {
            Assert.That(step.TryGetMember(key, out JsonValue value) && value.Kind == JsonKind.Number,
                Is.True, $"step 应含数字字段 {key}");
            return (ulong)value.IntValue;
        }

        private LoopbackEndpoint Ep(int seat) => seat == 0 ? _c0 : _c1;

        /// <summary>座位 0 的投影权威序列化（M13-T6：客户端视图与投影权威等值，非裸权威）。</summary>
        private string ProjectedAuthorityJson()
            => MatchStateSerializer.Serialize(PlayerViewProjector.Project(_controller.State, 0));

        /// <summary>
        /// 推进到恢复点：v1 应用 → 跳过 v2 → v3 判空洞 → 请求快照 → 基线重置为 v3；
        /// 返回 gap 期间被跳过/拒绝应用的旧步（step2、step3）原文供重放断言。
        /// </summary>
        private (JsonValue Step2, JsonValue Step3) RunToRecovery()
        {
            _controller = NewController();
            _host = new MatchHost(_controller);
            LoopbackLink.CreatePair(out _s0, out _c0);
            LoopbackLink.CreatePair(out _s1, out _c1);
            _host.Attach(_s0, 0);
            _host.Attach(_s1, 1);
            int first = _controller.State.ActivePlayerId;
            int second = 1 - first;
            _sync = NewSync(_host, _c0);
            // sync 只应用 c0（座位 0）端点收到的副本（M13-T6 起 step 按座位裁剪）
            JsonValue[] steps1 = BroadcastSteps(Ep(first), Ep(second), _host,
                CommandEnvelope(1, EndTurnPayload(first)));
            JsonValue step1 = first == 0 ? steps1[0] : steps1[1];
            Assert.That(_sync.ApplyStep(step1).Status, Is.EqualTo(StepApplyStatus.Applied), "v1 应应用");
            JsonValue[] steps2 = BroadcastSteps(Ep(second), Ep(first), _host,
                CommandEnvelope(1, EndTurnPayload(second))); // v2 被跳过（模拟丢包）
            JsonValue step2 = second == 0 ? steps2[0] : steps2[1];
            JsonValue[] steps3 = BroadcastSteps(Ep(first), Ep(second), _host,
                CommandEnvelope(2, EndTurnPayload(first)));
            JsonValue step3 = first == 0 ? steps3[0] : steps3[1];
            Assert.That(MemberNumber(step3, "basedOn"), Is.EqualTo(2UL));
            Assert.That(_sync.ApplyStep(step3).Status, Is.EqualTo(StepApplyStatus.VersionGap), "跳过 v2 应判空洞");

            _c0.Send(ClientStateSync.BuildSnapshotRequest(_sync.LocalVersion));
            _host.Pump();
            Assert.That(_c0.TryReceive(out string? snapRaw), Is.True, "快照请求应回快照");
            JsonValue snapshot = JsonValue.Parse(snapRaw!);
            Assert.That(snapshot.TryGetMember("version", out JsonValue v) && v.IntValue == 3, Is.True,
                "快照应携带当前权威版本 3");
            _sync.ApplySnapshot(snapshot);
            Assert.That(_sync.LocalVersion, Is.EqualTo(3UL));
            return (step2, step3);
        }

        [Test]
        public void SnapshotRequest_ReturnsCurrentProjectedSnapshot_Unicast()
        {
            // AC-1：请求含本地版本号；宿主回当前投影快照且仅单播请求端点
            _controller = NewController();
            _host = new MatchHost(_controller);
            LoopbackLink.CreatePair(out _s0, out _c0);
            LoopbackLink.CreatePair(out _s1, out _c1);
            _host.Attach(_s0, 0);
            _host.Attach(_s1, 1);
            int first = _controller.State.ActivePlayerId;
            NewSync(_host, _c0);
            BroadcastSteps(Ep(first), first == 0 ? _c1 : _c0, _host,
                CommandEnvelope(1, EndTurnPayload(first)));

            JsonValue request = JsonValue.Parse(ClientStateSync.BuildSnapshotRequest(1));
            Assert.That(request.TryGetMember("kind", out JsonValue kind)
                && kind.StringValue == "snapshot_request", Is.True, "请求 kind 应为 snapshot_request");
            Assert.That(request.TryGetMember("version", out JsonValue rv) && rv.IntValue == 1, Is.True,
                "请求应携带本地版本号");

            _c0.Send(request.ToJson());
            _host.Pump();
            Assert.That(_c0.TryReceive(out string? snapRaw), Is.True, "请求端点应收到快照");
            JsonValue snapshot = JsonValue.Parse(snapRaw!);
            Assert.That(snapshot.TryGetMember("version", out JsonValue v) && v.IntValue == 1, Is.True,
                "快照应携带当前权威版本 1");
            Assert.That(snapshot.TryGetMember("state", out JsonValue state)
                && state.Kind == JsonKind.Object, Is.True, "快照应含状态树");
            Assert.That(_c1.TryReceive(out _), Is.False, "快照应仅单播请求端点，不广播");
        }

        [Test]
        public void VersionGap_RecoversViaSnapshotRequest_AndDiscardsStaleSteps()
        {
            // AC-2 + AC-3：版本冲突经快照恢复一致；恢复后重放过期步被丢弃，状态与版本不变
            (JsonValue step2, JsonValue step3) = RunToRecovery();
            Assert.That(MatchStateSerializer.Serialize(_sync.State),
                Is.EqualTo(ProjectedAuthorityJson()), "恢复后视图应与投影权威一致");

            Assert.That(_sync.ApplyStep(step2).Status, Is.EqualTo(StepApplyStatus.DuplicateDiscarded),
                "过期步 v2 应被丢弃");
            Assert.That(_sync.ApplyStep(step3).Status, Is.EqualTo(StepApplyStatus.DuplicateDiscarded),
                "过期步 v3 应被丢弃");
            Assert.That(_sync.LocalVersion, Is.EqualTo(3UL), "丢弃过期步不得推进版本");
            Assert.That(MatchStateSerializer.Serialize(_sync.State),
                Is.EqualTo(ProjectedAuthorityJson()), "丢弃后视图仍与投影权威一致");
        }

        [Test]
        public void AfterRecovery_NewStepsApply()
        {
            // AC-4：恢复后下一条增量（v4，basedOn=3）正常应用，最终视图与权威一致。
            // seq=2：second 的收件箱在 RunToRecovery 已消费 seq=1（同 seq 同载荷会触发 T3 幂等重放而非广播）
            RunToRecovery();
            int next = _controller.State.ActivePlayerId;
            bool nextIs0 = next == 0;
            JsonValue[] steps4 = BroadcastSteps(Ep(next), nextIs0 ? _c1 : _c0, _host,
                CommandEnvelope(2, EndTurnPayload(next)));
            JsonValue step4 = nextIs0 ? steps4[0] : steps4[1]; // c0（座位 0）端点副本
            Assert.That(MemberNumber(step4, "basedOn"), Is.EqualTo(3UL), "新步应基于恢复后的版本");
            Assert.That(MemberNumber(step4, "version"), Is.EqualTo(4UL));

            StepOutcome outcome = _sync.ApplyStep(step4);
            Assert.That(outcome.Status, Is.EqualTo(StepApplyStatus.Applied), "恢复后新步应正常应用");
            Assert.That(_sync.LocalVersion, Is.EqualTo(4UL));
            Assert.That(MatchStateSerializer.Serialize(_sync.State),
                Is.EqualTo(ProjectedAuthorityJson()), "最终视图应与投影权威一致");
        }
    }
}
