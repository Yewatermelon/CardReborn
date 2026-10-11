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
    /// MatchHost 覆盖率补测（M11-T2）：错误路径 + 边界场景 + StateChangeSerializer 往返。
    /// </summary>
    [TestFixture]
    public class MatchHostCoverageTests
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
        // StateChangeSerializer 往返（补回 LoopbackMatchTests 覆盖）
        // -----------------------------------------------------------------

        [Test]
        public void StateChangeSerializer_WriteReadRoundtrip()
        {
            var diff = new List<StateChange>
            {
                new StateChange(ChangeKind.Modified, "phase", JsonValue.From(0), JsonValue.From(1)),
                new StateChange(ChangeKind.Modified, "turnNumber", JsonValue.From(1), JsonValue.From(2)),
            };
            JsonValue written = StateChangeSerializer.WriteList(diff);
            List<StateChange> readBack = StateChangeSerializer.ReadList(written);
            Assert.That(readBack.Count, Is.EqualTo(2));
            Assert.That(readBack[0].Path, Is.EqualTo("phase"));
            Assert.That(readBack[1].Path, Is.EqualTo("turnNumber"));
        }

        // -----------------------------------------------------------------
        // MatchHost 错误路径
        // -----------------------------------------------------------------

        [Test]
        public void MatchHost_Unknown上行Kind_ThrowsFormatException()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            clientEp.Send("{\"kind\":\"unknown\"}");
            Assert.Throws<FormatException>(() => host.Pump());
        }

        // M13-T2 语义演进：缺 payload 从抛 FormatException 改为结构化拒绝（状态零变更）
        [Test]
        public void MatchHost_CommandMissingPayload_ProducesRejectedStep()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp, 1);

            clientEp.Send("{\"kind\":\"command\"}");
            Assert.DoesNotThrow(() => host.Pump());
            Assert.That(host.StateVersion, Is.EqualTo(0));

            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True);
            JsonValue stepJson = JsonValue.Parse(stepRaw!);
            stepJson.TryGetMember("accepted", out JsonValue accepted);
            Assert.That(accepted.BoolValue, Is.False);
            stepJson.TryGetMember("error", out JsonValue error);
            Assert.That(error.StringValue, Is.EqualTo("UnknownCommand"));
        }

        [Test]
        public void Pump_WhenNoMessages_ReturnsZero()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            Assert.That(host.Pump(), Is.EqualTo(0));
        }

        [Test]
        public void MultipleCommands_StateVersionEqualsAcceptedCount()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            // M13-T2 座位归属校验：双端点各绑一个座位，按行动方发送
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);

            c0.Send("{\"kind\":\"hello\"}");
            c1.Send("{\"kind\":\"hello\"}");
            host.Pump();
            c0.TryReceive(out _);
            c1.TryReceive(out _);

            for (int i = 0; i < 3; i++)
            {
                LoopbackEndpoint actor = controller.State.ActivePlayerId == 0 ? c0 : c1;
                SendCommand(actor, new EndTurnCommand(controller.State.ActivePlayerId));
                host.Pump();
                c0.TryReceive(out _);
                c1.TryReceive(out _);
            }

            Assert.That(host.StateVersion, Is.EqualTo(3));
        }

        [Test]
        public void RejectedCommand_DoesNotAffectAcceptedCount()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            // M13-T2 座位归属校验：端点挂行动座位，拒绝走"非其回合"规则路径
            int activeSeat = controller.State.ActivePlayerId;
            host.Attach(serverEp, activeSeat);

            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            SendCommand(clientEp, new EndTurnCommand(activeSeat));
            host.Pump();
            clientEp.TryReceive(out _);
            Assert.That(host.StateVersion, Is.EqualTo(1));

            // 行动权已移交，同座位再次 EndTurn → RuleEngine 拒绝 NotYourTurn
            SendCommand(clientEp, new EndTurnCommand(activeSeat));
            host.Pump();
            clientEp.TryReceive(out _);
            Assert.That(host.StateVersion, Is.EqualTo(1));
        }

        [Test]
        public void MatchHost_IsFinished_ReflectsController()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            Assert.That(host.IsFinished, Is.False);

            // M13-T2 座位归属校验：双端点各绑一个座位，按行动方发送
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1);
            host.Attach(s0, 0);
            host.Attach(s1, 1);
            c0.Send("{\"kind\":\"hello\"}");
            c1.Send("{\"kind\":\"hello\"}");
            host.Pump();
            c0.TryReceive(out _);
            c1.TryReceive(out _);

            while (!controller.IsFinished)
            {
                LoopbackEndpoint actor = controller.State.ActivePlayerId == 0 ? c0 : c1;
                SendCommand(actor, new EndTurnCommand(controller.State.ActivePlayerId));
                host.Pump();
                c0.TryReceive(out _);
                c1.TryReceive(out _);
            }

            Assert.That(host.IsFinished, Is.True);
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
