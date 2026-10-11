using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Network;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// HostBridge 桥接测试（M13-T1）：网络 Command 帧 ↔ MatchHost 上行，
    /// MatchHost snapshot/step → 网络帧。覆盖 AC-2/AC-3/AC-4。
    /// </summary>
    [TestFixture]
    public class HostBridgeTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;

        private MatchController _controller = null!;
        private MatchHost _host = null!;
        private ITransport _netHost = null!;
        private ITransport _netClient = null!;
        private HostBridge _bridge = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        [SetUp]
        public void SetUp()
        {
            _controller = new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
            _host = new MatchHost(_controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint bridgeEp);
            _host.Attach(serverEp, 1);
            InMemoryTransport.CreatePair(out _netHost, out _netClient);
            _bridge = new HostBridge(bridgeEp, _netHost, seat: 1);
        }

        /// <summary>开局：注入 hello 上行 → 权威快照下行（并消费掉）。</summary>
        private void StartMatch()
        {
            _bridge.NotifyMatchStarted();
            _host.Pump();
            _bridge.PumpKernelToNetwork();
            Assert.That(_netClient.TryReceive(out byte[]? snap), Is.True, "开局快照应先到达");
            Assert.That(MessageCodec.TryUnframe(snap, 0, snap.Length, out _)!.Value.Type,
                Is.EqualTo(NetworkMessageType.MatchSnapshot));
        }

        private static string WriteUplink(IGameCommand command)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(command)))
                .ToJson();
        }

        private static RawMessage ReceiveFrame(ITransport transport)
        {
            Assert.That(transport.TryReceive(out byte[]? frame), Is.True, "应收到一帧");
            return MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
        }

        // -----------------------------------------------------------------
        // 开局快照：NotifyMatchStarted → hello 上行 → MatchSnapshot 帧下行
        // -----------------------------------------------------------------

        [Test]
        public void NotifyMatchStarted_SendsSnapshotFrame()
        {
            StartMatch();
            Assert.That(_bridge.PumpKernelToNetwork(), Is.EqualTo(0));
        }

        // -----------------------------------------------------------------
        // AC-2: Command 帧上行 → 权威接受 → StateDelta 帧下行（accepted=true）
        // -----------------------------------------------------------------

        [Test]
        public void CommandFrame_Accepted_StateDeltaFrameOut()
        {
            StartMatch();
            int activeId = _controller.State.ActivePlayerId;

            _bridge.ForwardCommand(new RawMessage(
                NetworkMessageType.Command,
                MessageCodec.CurrentProtocolVersion,
                WriteUplink(new EndTurnCommand(activeId))));
            _host.Pump();
            _bridge.PumpKernelToNetwork();

            RawMessage msg = ReceiveFrame(_netClient);
            Assert.That(msg.Type, Is.EqualTo(NetworkMessageType.StateDelta));
            JsonValue step = JsonValue.Parse(msg.PayloadJson);
            Assert.That(step.TryGetMember("kind", out JsonValue kind) && kind.StringValue == "step", Is.True);
            Assert.That(step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue, Is.True);
            Assert.That(_host.StateVersion, Is.EqualTo(1));
        }

        // -----------------------------------------------------------------
        // AC-3: 非行动方命令 → 拒绝，StateVersion 不变
        // -----------------------------------------------------------------

        [Test]
        public void CommandFrame_WrongPlayer_Rejected_VersionUnchanged()
        {
            StartMatch();
            int otherId = 1 - _controller.State.ActivePlayerId;

            _bridge.ForwardCommand(new RawMessage(
                NetworkMessageType.Command,
                MessageCodec.CurrentProtocolVersion,
                WriteUplink(new EndTurnCommand(otherId))));
            _host.Pump();
            _bridge.PumpKernelToNetwork();

            RawMessage msg = ReceiveFrame(_netClient);
            Assert.That(msg.Type, Is.EqualTo(NetworkMessageType.StateDelta));
            JsonValue step = JsonValue.Parse(msg.PayloadJson);
            Assert.That(step.TryGetMember("accepted", out JsonValue accepted) && !accepted.BoolValue, Is.True);
            Assert.That(_host.StateVersion, Is.EqualTo(0));
        }

        // -----------------------------------------------------------------
        // AC-4a: 畸形 JSON Command 帧 → 丢弃不抛，状态不变
        // -----------------------------------------------------------------

        [Test]
        public void CommandFrame_MalformedJson_DroppedNoThrow()
        {
            StartMatch();

            Assert.DoesNotThrow(() => _bridge.ForwardCommand(new RawMessage(
                NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, "{not valid json")));
            Assert.DoesNotThrow(() => _host.Pump());
            Assert.That(_bridge.PumpKernelToNetwork(), Is.EqualTo(0));
            Assert.That(_host.StateVersion, Is.EqualTo(0));
        }

        // -----------------------------------------------------------------
        // AC-4b: 未知 kind 的 Command 帧 → 丢弃，状态不变
        // -----------------------------------------------------------------

        [Test]
        public void CommandFrame_UnknownKind_DroppedNoThrow()
        {
            StartMatch();

            Assert.DoesNotThrow(() => _bridge.ForwardCommand(new RawMessage(
                NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, "{\"kind\":\"bogus\"}")));
            Assert.DoesNotThrow(() => _host.Pump());
            Assert.That(_bridge.PumpKernelToNetwork(), Is.EqualTo(0));
            Assert.That(_host.StateVersion, Is.EqualTo(0));
        }
    }
}
