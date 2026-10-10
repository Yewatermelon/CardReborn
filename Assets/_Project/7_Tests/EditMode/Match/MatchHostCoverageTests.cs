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

        [Test]
        public void MatchHost_CommandMissingPayload_ThrowsFormatException()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            clientEp.Send("{\"kind\":\"command\"}");
            Assert.Throws<FormatException>(() => host.Pump());
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

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            for (int i = 0; i < 3; i++)
            {
                SendCommand(clientEp, new EndTurnCommand(controller.State.ActivePlayerId));
                host.Pump();
                clientEp.TryReceive(out _);
            }

            Assert.That(host.StateVersion, Is.EqualTo(3));
        }

        [Test]
        public void RejectedCommand_DoesNotAffectAcceptedCount()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);

            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            int activeSeat = controller.State.ActivePlayerId;
            SendCommand(clientEp, new EndTurnCommand(activeSeat));
            host.Pump();
            clientEp.TryReceive(out _);
            Assert.That(host.StateVersion, Is.EqualTo(1));

            int nonActiveSeat = 1 - controller.State.ActivePlayerId;
            SendCommand(clientEp, new EndTurnCommand(nonActiveSeat));
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

            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            host.Attach(serverEp);
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            while (!controller.IsFinished)
            {
                SendCommand(clientEp, new EndTurnCommand(controller.State.ActivePlayerId));
                host.Pump();
                clientEp.TryReceive(out _);
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
