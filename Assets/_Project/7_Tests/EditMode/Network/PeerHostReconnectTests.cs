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
    /// 玩家 2 掉线重连测试（M13-T7 / AC-1…AC-4）：对局中断开后同座位重连——
    /// RequestSnapshot 帧化上行（M13-T5 移交项）、裁剪快照恢复、增量继续、
    /// 心跳超时判掉线后新连接接管。真实 TCP 回环 + 自旋等待（与 PeerHostTickTests 同模式）。
    /// </summary>
    [TestFixture]
    public class PeerHostReconnectTests
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

        /// <summary>房主（座位 0）从本地回环上行一条 EndTurn（仅座位 0 活跃时调用）。</summary>
        private void HostEndTurn()
        {
            JsonValue uplink = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(_peerHost.HostSeat))));
            _peerHost.LocalClientEndpoint.Send(uplink.ToJson());
        }

        /// <summary>指定座位经网络上行一条 EndTurn 命令帧。</summary>
        private static void SendEndTurnFrame(ITransport clientSide, int seat, int seq)
        {
            JsonValue uplink = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("seq", JsonValue.From(seq))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(seat))));
            clientSide.Send(MessageCodec.Frame(NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, uplink.ToJson()));
        }

        /// <summary>重连握手（Hello → HelloAck）。</summary>
        private void ReconnectHandshake(ITransport reconnect)
        {
            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2-reconnect"));
            reconnect.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
            Assert.That(ReceiveFrameAfterTick(reconnect).Type, Is.EqualTo(NetworkMessageType.HelloAck));
        }

        /// <summary>上行 RequestSnapshot 帧并断言回裁剪快照（座位 1 投影等值）；返回已载入的同步器。</summary>
        private ClientStateSync RequestSnapshotAndRecover(ITransport reconnect, ulong localVersion)
        {
            reconnect.Send(MessageCodec.Frame(
                NetworkMessageType.RequestSnapshot,
                MessageCodec.CurrentProtocolVersion,
                ClientStateSync.BuildSnapshotRequest(localVersion)));
            RawMessage snapFrame = ReceiveFrameAfterTick(reconnect);
            Assert.That(snapFrame.Type, Is.EqualTo(NetworkMessageType.MatchSnapshot), "重连请求应回全量快照");
            JsonValue snapshot = JsonValue.Parse(snapFrame.PayloadJson);
            Assert.That(snapshot.TryGetMember("version", out JsonValue v) && v.IntValue == (int)localVersion,
                Is.True, "快照版本应等于权威当前版本");

            ClientStateSync sync = new ClientStateSync();
            sync.ApplySnapshot(snapshot);
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(_controller.State, 1))),
                "重连快照应为座位 1 投影视图（重连不得绕过视野裁剪）");
            return sync;
        }

        // -----------------------------------------------------------------
        // AC-1: 重连恢复——裁剪快照 + 权威当前版本
        // -----------------------------------------------------------------

        [Test]
        public void Reconnect_RecoversTrimmedSnapshot_AtCurrentVersion()
        {
            StartMatch();
            HostEndTurn(); // seed 42 先手座位 0 → version 1，轮座位 1
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.StateDelta));
            Assert.That(_matchHost.StateVersion, Is.EqualTo(1UL));

            _client.Dispose();
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            Assert.That(_peerHost.AcceptPeer(), Is.Not.Null, "同座位新连接应被接受（重连接管）");
            ReconnectHandshake(_client);

            ClientStateSync sync = RequestSnapshotAndRecover(_client, 1UL);
            Assert.That(sync.LocalVersion, Is.EqualTo(1UL));
        }

        // -----------------------------------------------------------------
        // AC-2: 重连后增量继续——双向命令，客户端同步态与投影权威一致
        // -----------------------------------------------------------------

        [Test]
        public void Reconnect_IncrementsContinue_BothDirections()
        {
            StartMatch();
            HostEndTurn(); // version 1
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.StateDelta));

            _client.Dispose();
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            _peerHost.AcceptPeer();
            ReconnectHandshake(_client);
            ClientStateSync sync = RequestSnapshotAndRecover(_client, 1UL);

            // 座位 1（重连方）上行命令 → 接受 → version 2
            SendEndTurnFrame(_client, seat: 1, seq: 1);
            RawMessage delta = ReceiveFrameAfterTick(_client);
            Assert.That(delta.Type, Is.EqualTo(NetworkMessageType.StateDelta));
            JsonValue step = JsonValue.Parse(delta.PayloadJson);
            Assert.That(step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue, Is.True);
            Assert.That(sync.ApplyStep(step).Status, Is.EqualTo(StepApplyStatus.Applied));

            // 座位 0（房主本地）命令 → 座位 1 经网络收到增量 → version 3
            HostEndTurn();
            RawMessage delta2 = ReceiveFrameAfterTick(_client);
            Assert.That(sync.ApplyStep(JsonValue.Parse(delta2.PayloadJson)).Status,
                Is.EqualTo(StepApplyStatus.Applied));

            Assert.That(_matchHost.StateVersion, Is.EqualTo(3UL));
            Assert.That(sync.LocalVersion, Is.EqualTo(_matchHost.StateVersion));
            Assert.That(MatchStateSerializer.Serialize(sync.State), Is.EqualTo(
                MatchStateSerializer.Serialize(PlayerViewProjector.Project(_controller.State, 1))));
        }

        // -----------------------------------------------------------------
        // AC-4: 心跳超时判掉线后，新连接仍可接管并恢复
        // -----------------------------------------------------------------

        [Test]
        public void Reconnect_AfterHeartbeatTimeout_NewConnectionTakesOver()
        {
            StartMatch();
            _clock.Advance(PeerHost.TimeoutTicks);
            _peerHost.RunNetworkTick();
            Assert.That(_session!.TimedOut, Is.True, $"{PeerHost.TimeoutTicks} tick 无响应应判掉线");

            _client.Dispose();
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            PeerSession? reconnected = _peerHost.AcceptPeer();
            Assert.That(reconnected, Is.Not.Null);
            Assert.That(reconnected!.TimedOut, Is.False, "新会话应为健康会话");
            ReconnectHandshake(_client);

            ClientStateSync sync = RequestSnapshotAndRecover(_client, 0UL);
            Assert.That(sync.LocalVersion, Is.EqualTo(0UL));
        }

        // -----------------------------------------------------------------
        // M14-T3-B5: 重连接管必须重置座位序号基线——走真实接管路径（PeerHost.AcceptPeer）。
        // 断线前座位 1 已消费 seq=1；重连客户端（新进程）seq 从 1 重新计数，必须重新可执行。
        // 修复前缺陷：TakeOverPeer 以 HostBridge.KernelEndpoint 摘除旧端点，而该属性实持
        // 桥侧端点（权威挂载的是另一侧）→ Detach 查无此端点成无操作 → 基线残留 →
        // 同 seq 同载荷被幂等重放拦截（状态不推进），异载荷全部 StaleSequence。
        // -----------------------------------------------------------------

        [Test]
        public void Reconnect_TakeoverResetsInboxBaseline_NewClientSeq1Executable()
        {
            StartMatch();
            HostEndTurn(); // version 1，轮座位 1
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.StateDelta));

            // 断线前座位 1 消费 seq=1（合法 EndTurn，被接受 → version 2，轮回座位 0）
            SendEndTurnFrame(_client, seat: 1, seq: 1);
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.StateDelta));
            Assert.That(_matchHost.StateVersion, Is.EqualTo(2UL));

            _client.Dispose();
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            Assert.That(_peerHost.AcceptPeer(), Is.Not.Null, "同座位新连接应被接受（重连接管）");
            ReconnectHandshake(_client);
            RequestSnapshotAndRecover(_client, 2UL);

            // 座位 0 结束回合 → 轮回座位 1（version 3）
            HostEndTurn();
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.StateDelta));

            // 重连客户端 seq=1 必须重新可执行：接受且权威版本推进到 4
            //（修复前：基线残留 seq=1 同载荷命中幂等回执，状态停留 version 3）
            SendEndTurnFrame(_client, seat: 1, seq: 1);
            RawMessage delta = ReceiveFrameAfterTick(_client);
            JsonValue step = JsonValue.Parse(delta.PayloadJson);
            Assert.That(step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue,
                Is.True, "重连后同座位 seq=1 应重新可执行（收件箱基线已随接管重置）");
            Assert.That(_matchHost.StateVersion, Is.EqualTo(4UL), "重连后命令应真实执行（版本推进）");
        }
    }
}
