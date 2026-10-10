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
    /// PeerSession 心跳与超时测试（M12-T7）：ManualClock 驱动 tick 流逝。
    /// </summary>
    [TestFixture]
    public class PeerSessionHeartbeatTests
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
        public void NoIdle_NoPingSent()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 推进 tick 但不超过心跳阈值
            _clock.Advance(10);
            session.Pump();

            Assert.That(peerSide.TryReceive(out byte[]? _), Is.False);
        }

        [Test]
        public void IdleExceedsHeartbeatThreshold_SendsPing()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 先握手——心跳只在握手后发
            SendHello(peerSide);
            session.Pump();
            peerSide.TryReceive(out _); // 消费 HelloAck

            // 推进 tick 超过心跳阈值（30 tick）
            _clock.Advance(35);
            session.Pump();

            Assert.That(peerSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage result = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)!.Value;
            Assert.That(result.Type, Is.EqualTo(NetworkMessageType.Ping));
        }

        [Test]
        public void PeerPing_TriggersPong()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            JsonValue ping = JsonValue.Object()
                .Add("timestamp", JsonValue.From(12345));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Ping, MessageCodec.CurrentProtocolVersion, ping.ToJson()));

            _clock.Advance(5);
            session.Pump();

            Assert.That(peerSide.TryReceive(out byte[]? frame), Is.True);
            RawMessage result = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)!.Value;
            Assert.That(result.Type, Is.EqualTo(NetworkMessageType.Pong));
        }

        [Test]
        public void IdleExceedsTimeoutTicks_MarksTimedOut()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 推进 tick 超过超时阈值（60 tick）
            _clock.Advance(70);
            session.Pump();

            Assert.That(session.TimedOut, Is.True);
            Assert.That(session.IsConnected, Is.False);
            Assert.That(hostSide.Connected, Is.False);
        }

        [Test]
        public void ActivityResetsTimeout()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 先推进到心跳阈值前
            _clock.Advance(25);

            // 对手发一条消息——重置活动时间
            SendHello(peerSide);
            session.Pump();

            // 再推进 35 tick——距离上次活动是 35，还没到超时
            _clock.Advance(35);
            session.Pump();

            Assert.That(session.TimedOut, Is.False);
            Assert.That(session.IsConnected, Is.True);
        }

        [Test]
        public void PongResetsTimeout()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 先发 Pong
            JsonValue pong = JsonValue.Object()
                .Add("timestamp", JsonValue.From(99));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Pong, MessageCodec.CurrentProtocolVersion, pong.ToJson()));

            _clock.Advance(55); // 距上次活动 55 tick，还没超时
            session.Pump();

            Assert.That(session.TimedOut, Is.False);
        }

        [Test]
        public void HeartbeatNotSentBeforeHandshake()
        {
            InMemoryTransport.CreatePair(out ITransport hostSide, out ITransport peerSide);
            var session = new PeerSession(_matchHost, hostSide, _clock, peerSeat: 1);

            // 还没握手就 idle 超过心跳阈值
            _clock.Advance(40);
            session.Pump();

            Assert.That(peerSide.TryReceive(out byte[]? _), Is.False);
        }

        private static void SendHello(ITransport peerSide)
        {
            JsonValue hello = JsonValue.Object()
                .Add("playerName", JsonValue.From("opponent"));
            peerSide.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
        }
    }
}
