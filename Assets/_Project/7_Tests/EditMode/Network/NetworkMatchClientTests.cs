using Card.Application.Match;
using Card.Core;
using Card.Network;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// 加入方客户端会话测试（M14-T3，Docs/05 §3.4 NetworkMatchClient）：
    /// 握手 / Ready / 下行分发 / 命令上行 / 心跳应答 / 防御姿态（畸形帧、版本门、断链）。
    /// 宿主侧用真实 PeerSession（InMemory 配对）或手工帧注入。
    /// </summary>
    [TestFixture]
    public class NetworkMatchClientTests
    {
        private MatchController _controller = null!;
        private MatchHost _matchHost = null!;
        private ManualClock _clock = null!;
        private ITransport _hostSide = null!;
        private ITransport _clientSide = null!;
        private PeerSession _session = null!;
        private NetworkMatchClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            var database = MatchControllerFixtures.BuildDatabase();
            var deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            _controller = new MatchController(
                MatchControllerFixtures.NewMatch(database, deck, seed: 42), database);
            _clock = new ManualClock();
            _matchHost = new MatchHost(_controller);

            InMemoryTransport.CreatePair(out _hostSide, out _clientSide);
            _session = new PeerSession(_matchHost, _hostSide, _clock, peerSeat: 1);
            _client = new NetworkMatchClient(_clientSide);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
        }

        // -----------------------------------------------------------------
        // 握手与开局
        // -----------------------------------------------------------------

        [Test]
        public void SendHello_HostAck_RaisesHelloAckAndHandshook()
        {
            JsonValue? ackPayload = null;
            _client.HelloAckReceived += payload => ackPayload = payload;

            _client.SendHello("玩家2");
            _session.Pump();
            _client.Pump();

            Assert.That(_client.Handshook, Is.True);
            Assert.That(ackPayload, Is.Not.Null);
            Assert.That(_client.Connected, Is.True);
        }

        [Test]
        public void SendHello_VersionMismatch_ErrorThenDisconnected()
        {
            // 宿主侧换成版本不匹配的会话：直接用手工 Error 帧 + 断链模拟 PeerSession 行为
            _hostSide.Send(Frame(NetworkMessageType.Error, "{\"code\":\"ProtocolVersionMismatch\"}"));
            _hostSide.Dispose();

            string? errorCode = null;
            bool disconnected = false;
            _client.ErrorReceived += code => errorCode = code;
            _client.Disconnected += _ => disconnected = true;

            _client.Pump();

            Assert.That(errorCode, Is.EqualTo("ProtocolVersionMismatch"));
            Assert.That(disconnected, Is.True);
        }

        [Test]
        public void SendReady_AfterHandshake_MatchStartReceived()
        {
            _client.SendHello("玩家2");
            _session.Pump();
            _client.Pump();

            bool matchStarted = false;
            _client.MatchStartReceived += _ => matchStarted = true;

            _client.SendReady();
            _session.Pump();
            _client.Pump();

            Assert.That(matchStarted, Is.True);
            Assert.That(_client.MatchStarted, Is.True);
        }

        // -----------------------------------------------------------------
        // 下行分发
        // -----------------------------------------------------------------

        [Test]
        public void SnapshotFrame_RaisesSnapshotReceived()
        {
            JsonValue? snapshot = null;
            _client.SnapshotReceived += payload => snapshot = payload;

            _hostSide.Send(Frame(NetworkMessageType.MatchSnapshot,
                "{\"kind\":\"snapshot\",\"version\":3}"));
            _client.Pump();

            Assert.That(snapshot, Is.Not.Null);
            snapshot!.TryGetMember("kind", out JsonValue kind);
            Assert.That(kind.StringValue, Is.EqualTo("snapshot"));
            snapshot.TryGetMember("version", out JsonValue version);
            Assert.That(version.IntValue, Is.EqualTo(3));
        }

        [Test]
        public void StepFrame_RaisesStepReceived()
        {
            JsonValue? step = null;
            _client.StepReceived += payload => step = payload;

            _hostSide.Send(Frame(NetworkMessageType.StateDelta, SampleStepJson()));
            _client.Pump();

            Assert.That(step, Is.Not.Null);
            step!.TryGetMember("kind", out JsonValue kind);
            Assert.That(kind.StringValue, Is.EqualTo("step"));
        }

        [Test]
        public void MatchEndFrame_ParsesWinnerAndReason()
        {
            int? winner = null;
            string? reason = null;
            _client.MatchEnded += (w, r) => { winner = w; reason = r; };

            _hostSide.Send(Frame(NetworkMessageType.MatchEnd,
                "{\"winner\":1,\"reason\":\"Disconnect\"}"));
            _client.Pump();

            Assert.That(winner, Is.EqualTo(1));
            Assert.That(reason, Is.EqualTo("Disconnect"));
        }

        [Test]
        public void MatchEndFrame_NullWinner_ParseAsForfeit()
        {
            int? winner = 0;
            _client.MatchEnded += (w, _) => winner = w;

            _hostSide.Send(Frame(NetworkMessageType.MatchEnd,
                "{\"winner\":null,\"reason\":\"HostQuit\"}"));
            _client.Pump();

            Assert.That(winner, Is.Null);
        }

        [Test]
        public void ErrorFrame_ParsesCodeWithoutDisconnect()
        {
            string? errorCode = null;
            _client.ErrorReceived += code => errorCode = code;

            _hostSide.Send(Frame(NetworkMessageType.Error, "{\"code\":\"Sample\"}"));
            _client.Pump();

            Assert.That(errorCode, Is.EqualTo("Sample"));
            Assert.That(_client.Connected, Is.True);
        }

        [Test]
        public void DownlinkVersionMismatch_DroppedStillConnected()
        {
            bool anyEvent = false;
            _client.SnapshotReceived += _ => anyEvent = true;
            _client.Disconnected += _ => anyEvent = true;

            _hostSide.Send(MessageCodec.Frame(
                NetworkMessageType.MatchSnapshot, 999, "{\"kind\":\"snapshot\",\"version\":3}"));
            _client.Pump();

            Assert.That(anyEvent, Is.False);
            Assert.That(_client.Connected, Is.True);
        }

        // -----------------------------------------------------------------
        // 上行与心跳
        // -----------------------------------------------------------------

        [Test]
        public void SendCommand_ForwardsEnvelopeAsCommandFrame()
        {
            const string envelope = "{\"kind\":\"command\",\"payload\":{\"type\":\"EndTurn\"},\"seq\":2}";

            _client.SendCommand(envelope);

            Assert.That(_hostSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage msg = MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
            Assert.That(msg.Type, Is.EqualTo(NetworkMessageType.Command));
            Assert.That(msg.PayloadJson, Is.EqualTo(envelope));
        }

        [Test]
        public void HostPing_ClientAutoPong_EchoesTimestamp()
        {
            _hostSide.Send(Frame(NetworkMessageType.Ping, "{\"timestamp\":123}"));
            _client.Pump();

            Assert.That(_hostSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage pong = MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
            Assert.That(pong.Type, Is.EqualTo(NetworkMessageType.Pong));
            JsonValue payload = JsonValue.Parse(pong.PayloadJson);
            payload.TryGetMember("timestamp", out JsonValue ts);
            Assert.That(ts.IntValue, Is.EqualTo(123));
        }

        // -----------------------------------------------------------------
        // 防御姿态
        // -----------------------------------------------------------------

        [Test]
        public void UnknownFrameType_IgnoredStillConnected()
        {
            bool disconnected = false;
            _client.Disconnected += _ => disconnected = true;

            _hostSide.Send(MessageCodec.Frame((NetworkMessageType)99,
                MessageCodec.CurrentProtocolVersion, "{}"));
            _client.Pump();

            Assert.That(disconnected, Is.False);
            Assert.That(_client.Connected, Is.True);
        }

        [Test]
        public void MalformedFrame_DisconnectsAndStopsDispatch()
        {
            bool disconnected = false;
            JsonValue? snapshot = null;
            _client.Disconnected += _ => disconnected = true;
            _client.SnapshotReceived += payload => snapshot = payload;

            _hostSide.Send(new byte[] { 1, 2, 3 }); // 不足 4 字节前缀的垃圾
            _hostSide.Send(Frame(NetworkMessageType.MatchSnapshot, "{\"kind\":\"snapshot\"}"));
            _client.Pump();
            _client.Pump();

            Assert.That(disconnected, Is.True);
            Assert.That(snapshot, Is.Null); // 畸形帧后不再分发后续帧
        }

        [Test]
        public void PeerDispose_RaisesDisconnected()
        {
            bool disconnected = false;
            _client.Disconnected += _ => disconnected = true;

            _hostSide.Dispose();
            _client.Pump();

            Assert.That(disconnected, Is.True);
        }

        [Test]
        public void PumpAfterDispose_NoThrowNoEvent()
        {
            _client.Dispose();

            bool anyEvent = false;
            _client.SnapshotReceived += _ => anyEvent = true;
            Assert.DoesNotThrow(() => _client.Pump());
            Assert.That(anyEvent, Is.False);
        }

        // -----------------------------------------------------------------
        // 辅助
        // -----------------------------------------------------------------

        private static byte[] Frame(NetworkMessageType type, string payloadJson)
        {
            return MessageCodec.Frame(type, MessageCodec.CurrentProtocolVersion, payloadJson);
        }

        private static string SampleStepJson()
        {
            return "{\"kind\":\"step\",\"version\":4,\"basedOn\":3,\"accepted\":true," +
                   "\"error\":\"None\",\"detail\":\"\",\"finished\":false," +
                   "\"changes\":[],\"events\":[]}";
        }
    }
}
