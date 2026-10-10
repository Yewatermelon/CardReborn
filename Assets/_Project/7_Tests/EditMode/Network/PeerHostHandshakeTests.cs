using System;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Network;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// PeerSession 握手流程测试（M12-T5+T6）：Hello→HelloAck→Ready→MatchStart。
    /// 用 InMemoryTransport 配对模拟房主侧与对手客户端。
    /// </summary>
    [TestFixture]
    public class PeerHostHandshakeTests
    {
        private MatchController _controller = null!;
        private MatchHost _matchHost = null!;
        private ManualClock _clock = null!;

        [OneTimeSetUp]
        public void SetUp()
        {
            var database = MatchControllerFixtures.BuildDatabase();
            var deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            _controller = new MatchController(
                MatchControllerFixtures.NewMatch(database, deck, seed: 42), database);
            _clock = new ManualClock();
            _matchHost = new MatchHost(_controller);
        }

        [Test]
        public void HelloHelloAck_ProtocolVersionMismatch_ReturnsError()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 对手发 Hello（协议版本 999 不匹配）
            JsonValue hello = JsonValue.Object()
                .Add("playerName", JsonValue.From("opponent"));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Hello, 999, hello.ToJson()));

            session.Pump();

            // 对手应该收到 Error
            Assert.That(peerSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage result = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)!.Value;
            Assert.That(result.Type, Is.EqualTo(NetworkMessageType.Error));
            Assert.That(result.ProtocolVersion, Is.EqualTo(MessageCodec.CurrentProtocolVersion));
        }

        [Test]
        public void HelloHelloAck_ProtocolMatch_HandshakeCompleted()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 对手发 Hello（协议版本正确）
            JsonValue hello = JsonValue.Object()
                .Add("playerName", JsonValue.From("opponent"));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));

            session.Pump();

            // 对手应该收到 HelloAck
            Assert.That(peerSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage result = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)!.Value;
            Assert.That(result.Type, Is.EqualTo(NetworkMessageType.HelloAck));
            Assert.That(result.ProtocolVersion, Is.EqualTo(MessageCodec.CurrentProtocolVersion));

            // 会话状态
            Assert.That(session.Handshook, Is.True);
        }

        [Test]
        public void ReadyAfterHello_MatchStartSent()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 先 Hello
            JsonValue hello = JsonValue.Object()
                .Add("playerName", JsonValue.From("opponent"));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
            session.Pump();
            peerSide.TryReceive(out _); // 消费 HelloAck

            // 再 Ready
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}"));
            session.Pump();

            // 对手应该收到 MatchStart
            Assert.That(peerSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage result = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)!.Value;
            Assert.That(result.Type, Is.EqualTo(NetworkMessageType.MatchStart));
            Assert.That(session.PeerReady, Is.True);
        }

        [Test]
        public void LeaveMessage_DisposesTransport()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Leave, MessageCodec.CurrentProtocolVersion, "{}"));
            session.Pump();

            Assert.That(hostSide.Connected, Is.False);
        }

        [Test]
        public void PeerSession_UnknownCommand_DuringHandshake_Ignored()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 在 Hello 之前发 Command——应该被忽略，不崩溃
            JsonValue cmd = JsonValue.Object()
                .Add("type", JsonValue.From("EndTurn"))
                .Add("playerId", JsonValue.From(0));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, cmd.ToJson()));
            Assert.DoesNotThrow(() => session.Pump());
        }
    }
}
