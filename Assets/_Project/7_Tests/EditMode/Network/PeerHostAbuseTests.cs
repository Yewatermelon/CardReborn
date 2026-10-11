using System;
using System.Collections.Generic;
using System.Threading;
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
    /// 消息异常注入矩阵·宿主端到端（M13-T9 / AC-6）：真实 TCP 回环下依次注入
    /// 未知类型、S→C 类型上行、版本错帧、越座位命令——RunNetworkTick 不抛异常、
    /// 不崩溃不假活，合法命令仍走完"校验→结算→step 下发"链路。
    /// </summary>
    [TestFixture]
    public class PeerHostAbuseTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;
        private MatchController _controller = null!;
        private MatchHost _matchHost = null!;
        private PeerHost _peerHost = null!;
        private ManualClock _clock = null!;
        private ITransport _client = null!;
        private PeerSession? _session;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _peerHost?.Dispose();
        }

        /// <summary>反复驱动网络 tick 直到指定端收到一帧（上限 2 秒）。</summary>
        private RawMessage ReceiveFrameAfterTick(ITransport clientSide)
        {
            for (int i = 0; i < 200; i++)
            {
                _peerHost.RunNetworkTick();
                if (clientSide.TryReceive(out byte[]? frame))
                {
                    return MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
                }

                Thread.Sleep(10);
            }

            Assert.Fail("等待网络帧超时（2 秒）。");
            throw new InvalidOperationException("unreachable");
        }

        /// <summary>标准开局：握手 → Ready → MatchStart + 初始快照（version 0）。</summary>
        private void StartMatch()
        {
            _controller = new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
            _matchHost = new MatchHost(_controller);
            _clock = new ManualClock();
            _peerHost = new PeerHost(_matchHost, _clock);
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            _session = _peerHost.AcceptPeer();
            Assert.That(_session, Is.Not.Null);

            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2"));
            _client.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.HelloAck));
            _client.Send(MessageCodec.Frame(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}"));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.MatchStart));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.MatchSnapshot));
        }

        /// <summary>注入若干帧后驱动数轮网络 tick——断言全程不抛异常（不崩溃）。</summary>
        private void InjectAndTick(params byte[][] frames)
        {
            foreach (byte[] frame in frames)
            {
                _client.Send(frame);
            }

            for (int i = 0; i < 5; i++)
            {
                Assert.DoesNotThrow(() => _peerHost.RunNetworkTick(), "异常注入不得打死宿主 tick");
                Thread.Sleep(5);
            }
        }

        // -----------------------------------------------------------------
        // AC-6: 注入后宿主不崩溃不假活——合法命令仍走完整链路
        // -----------------------------------------------------------------

        [Test]
        public void InjectedFrames_HostSurvives_LegalCommandStillProcessed()
        {
            StartMatch();
            ulong versionBefore = _matchHost.StateVersion;
            int activeSeat = _controller.View.ActivePlayerId;

            // 注入矩阵（对端上行）：
            // ① 未知类型帧；② S→C 专属类型（StateDelta）；③ 版本不匹配 Command；
            // ④ 越座位 Command（对手座位冒用行动方身份）
            JsonValue goodEnvelope = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(activeSeat))));
            JsonValue forgedEnvelope = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(1 - activeSeat))));
            InjectAndTick(
                MessageCodec.Frame((NetworkMessageType)99, MessageCodec.CurrentProtocolVersion, "{}"),
                MessageCodec.Frame(NetworkMessageType.StateDelta, MessageCodec.CurrentProtocolVersion, "{}"),
                MessageCodec.Frame(NetworkMessageType.Command, 999, goodEnvelope.ToJson()),
                MessageCodec.Frame(NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, forgedEnvelope.ToJson()));

            Assert.That(_matchHost.StateVersion, Is.EqualTo(versionBefore), "注入帧不得改变权威状态");
            Assert.That(_session!.IsConnected, Is.True, "可恢复违规不应断链");

            // 注入后合法命令仍走完链路（动态行动方：座位 1 经网络、座位 0 经本地回环）
            if (activeSeat == 1)
            {
                _client.Send(MessageCodec.Frame(
                    NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, goodEnvelope.ToJson()));
                RawMessage step = ReceiveFrameAfterTick(_client);
                Assert.That(step.Type, Is.EqualTo(NetworkMessageType.StateDelta), "合法命令应收到权威 step 回执");
            }
            else
            {
                JsonValue uplink = JsonValue.Object()
                    .Add("kind", JsonValue.From("command"))
                    .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(_peerHost.HostSeat))));
                _peerHost.LocalClientEndpoint.Send(uplink.ToJson());
                RawMessage step = ReceiveFrameAfterTick(_client);
                Assert.That(step.Type, Is.EqualTo(NetworkMessageType.StateDelta), "合法命令应收到权威 step 回执");
            }

            Assert.That(_matchHost.StateVersion, Is.EqualTo(versionBefore + 1), "合法命令应推进权威版本");
        }
    }
}
