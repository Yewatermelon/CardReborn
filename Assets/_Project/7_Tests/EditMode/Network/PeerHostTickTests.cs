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
    /// PeerHost 网络编排测试（M13-T1 / AC-5）：RunNetworkTick 按
    /// 会话泵 → 权威推进 → 桥接下行的顺序驱动；空转无副作用。
    /// 真实 TCP 回环 + 自旋等待（接收线程异步入队，与 M12-T3 测试同模式）。
    /// </summary>
    [TestFixture]
    public class PeerHostTickTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        /// <summary>反复驱动网络 tick 直到客户端收到一帧（上限 2 秒）。</summary>
        private static RawMessage ReceiveFrameAfterTick(PeerHost peerHost, ITransport clientSide)
        {
            for (int i = 0; i < 200; i++)
            {
                peerHost.RunNetworkTick();
                if (clientSide.TryReceive(out byte[]? frame))
                {
                    return MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
                }

                Thread.Sleep(10);
            }

            Assert.Fail("等待网络帧超时（2 秒）。");
            throw new InvalidOperationException("unreachable");
        }

        // -----------------------------------------------------------------
        // AC-5a: 握手 → Ready → 快照 → 命令上行 → StateDelta 全链路（真实 TCP 回环）
        // -----------------------------------------------------------------

        [Test]
        public void RunNetworkTick_FullPipeline_SnapshotThenStateDelta()
        {
            MatchController controller = new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
            MatchHost matchHost = new MatchHost(controller);

            using PeerHost peerHost = new PeerHost(matchHost, new ManualClock());
            using ITransport clientSide = BclTcpTransport.Connect("127.0.0.1", peerHost.ListenPort);
            PeerSession session = peerHost.AcceptPeer()!;
            Assert.That(session, Is.Not.Null);

            // Hello → HelloAck
            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2"));
            clientSide.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
            Assert.That(ReceiveFrameAfterTick(peerHost, clientSide).Type, Is.EqualTo(NetworkMessageType.HelloAck));
            Assert.That(session.Handshook, Is.True);

            // Ready → MatchStart + 初始快照（NotifyMatchStarted 的 hello 上行由权威推进处理）
            clientSide.Send(MessageCodec.Frame(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}"));
            Assert.That(ReceiveFrameAfterTick(peerHost, clientSide).Type, Is.EqualTo(NetworkMessageType.MatchStart));
            Assert.That(session.MatchStarted, Is.True);
            Assert.That(ReceiveFrameAfterTick(peerHost, clientSide).Type, Is.EqualTo(NetworkMessageType.MatchSnapshot));

            // 命令上行 → StateDelta
            int activeId = controller.State.ActivePlayerId;
            JsonValue uplink = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(activeId))));
            clientSide.Send(MessageCodec.Frame(NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, uplink.ToJson()));
            RawMessage delta = ReceiveFrameAfterTick(peerHost, clientSide);

            Assert.That(delta.Type, Is.EqualTo(NetworkMessageType.StateDelta));
            JsonValue step = JsonValue.Parse(delta.PayloadJson);
            Assert.That(step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue, Is.True);
            Assert.That(matchHost.StateVersion, Is.EqualTo(1));
        }

        // -----------------------------------------------------------------
        // AC-5b: 空转 tick——无会话/无消息时无副作用、不产帧
        // -----------------------------------------------------------------

        [Test]
        public void RunNetworkTick_Idle_NoSideEffects()
        {
            MatchController controller = new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
            MatchHost matchHost = new MatchHost(controller);

            using PeerHost peerHost = new PeerHost(matchHost, new ManualClock());
            Assert.DoesNotThrow(() => peerHost.RunNetworkTick()); // 无会话空转

            using ITransport clientSide = BclTcpTransport.Connect("127.0.0.1", peerHost.ListenPort);
            Assert.That(peerHost.AcceptPeer(), Is.Not.Null);

            // 就绪前不发任何消息——等待一段时间后仍不应有帧
            for (int i = 0; i < 20; i++)
            {
                peerHost.RunNetworkTick();
                Thread.Sleep(10);
            }

            Assert.That(clientSide.TryReceive(out _), Is.False, "空转不应产生任何帧");
            Assert.That(matchHost.StateVersion, Is.EqualTo(0));
        }
    }
}
