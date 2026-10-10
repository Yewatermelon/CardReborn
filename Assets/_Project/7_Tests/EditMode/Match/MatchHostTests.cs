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
            Assert.That(MatchStateSerializer.Serialize(restored), Is.EqualTo(MatchStateSerializer.Serialize(controller.State)));
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
            host.Attach(serverEp);

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
            host.Attach(s0);
            host.Attach(s1);

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
        // AC-7: PlayerViewProjector 默认 identity
        // -----------------------------------------------------------------

        [Test]
        public void PlayerViewProjector_Identity_RoundtripMatches()
        {
            MatchController controller = NewController();
            MatchState projected = PlayerViewProjector.Project(controller.State, observerPlayerId: 0);
            Assert.That(
                MatchStateSerializer.Serialize(projected),
                Is.EqualTo(MatchStateSerializer.Serialize(controller.State)));
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
    }
}
