using System;
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
    /// 消息异常注入矩阵·会话层（M13-T9 / AC-1…AC-5）：乱序（未握手 Ready）、
    /// 重复（Hello）、版本不匹配（非 Hello 帧）、非法字段（S→C 类型上行）、
    /// 畸形帧——全部拒绝且不崩溃。InMemoryTransport 注入帧，精准无时序抖动。
    /// </summary>
    [TestFixture]
    public class PeerSessionAbuseTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;
        private MatchController _controller = null!;
        private MatchHost _matchHost = null!;
        private ManualClock _clock = null!;
        private ITransport _hostSide = null!;
        private ITransport _peerSide = null!;
        private PeerSession _session = null!;
        private HostBridge _bridge = null!;
        private NetworkDiagnostics _diag = null!;

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
            _matchHost = new MatchHost(_controller);
            _clock = new ManualClock();
            _diag = new NetworkDiagnostics(_clock);
            InMemoryTransport.CreatePair(out _hostSide, out _peerSide);

            LoopbackLink.CreatePair(out LoopbackEndpoint kernelEp, out LoopbackEndpoint bridgeEp);
            _matchHost.Attach(kernelEp, 1);
            _bridge = new HostBridge(bridgeEp, _hostSide, seat: 1);
            _session = new PeerSession(_matchHost, _hostSide, _clock, peerSeat: 1, bridge: _bridge, diag: _diag);
        }

        [TearDown]
        public void TearDown()
        {
            _hostSide.Dispose();
            _peerSide.Dispose();
        }

        /// <summary>对端上行一条帧。</summary>
        private void SendFromPeer(NetworkMessageType type, int version, string payload)
        {
            _peerSide.Send(MessageCodec.Frame(type, version, payload));
        }

        /// <summary>对端上行原始字节（畸形帧注入）。</summary>
        private void SendRawFromPeer(byte[] raw)
        {
            _peerSide.Send(raw);
        }

        /// <summary>标准握手开局：Hello → Ready（对端视角发送，会话 Pump 驱动，权威 Pump 下行快照）。</summary>
        private void StartMatch()
        {
            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2"));
            SendFromPeer(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson());
            PumpAll();
            Assert.That(_session.Handshook, Is.True, "握手应完成");

            SendFromPeer(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}");
            PumpAll();
            Assert.That(_session.MatchStarted, Is.True, "对局应已激活");
            DrainPeer(); // 清掉 MatchStart / MatchSnapshot 下行，后续断言只看新增帧
        }

        /// <summary>驱动一轮完整管线：会话 Pump → 权威 Pump → 桥接下行。</summary>
        private void PumpAll()
        {
            _session.Pump();
            _matchHost.Pump();
            _bridge.PumpKernelToNetwork();
        }

        /// <summary>清空对端收帧队列。</summary>
        private void DrainPeer()
        {
            while (_peerSide.TryReceive(out byte[]? _))
            {
            }
        }

        // -----------------------------------------------------------------
        // AC-1: 乱序·未握手 Ready → 丢弃且会话可用
        // -----------------------------------------------------------------

        [Test]
        public void ReadyBeforeHandshake_Dropped_SessionStillUsable()
        {
            SendFromPeer(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}");
            _session.Pump();

            Assert.That(_session.PeerReady, Is.False, "未握手 Ready 应被丢弃");
            Assert.That(_peerSide.TryReceive(out byte[]? frame), Is.False, "不应下发 MatchStart");
            Assert.That(_session.IsConnected, Is.True, "可恢复违规不应断链");

            // 后续正常握手仍可完成——会话未被破坏
            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2"));
            SendFromPeer(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson());
            _session.Pump();
            Assert.That(_session.Handshook, Is.True, "注入后正常握手仍应完成");
        }

        // -----------------------------------------------------------------
        // AC-2: 重复·重复 Hello → 幂等（不重发 HelloAck）
        // -----------------------------------------------------------------

        [Test]
        public void DuplicateHello_Idempotent_NoSecondHelloAck()
        {
            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2"));
            SendFromPeer(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson());
            _session.Pump();
            Assert.That(_peerSide.TryReceive(out byte[]? ack1) && ack1 != null &&
                MessageCodec.TryUnframe(ack1, 0, ack1.Length, out _)!.Value.Type == NetworkMessageType.HelloAck,
                Is.True, "首次 Hello 应回 HelloAck");

            SendFromPeer(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson());
            _session.Pump();
            Assert.That(_peerSide.TryReceive(out byte[]? _), Is.False, "重复 Hello 不应重发 HelloAck");

            // 后续流程不受影响
            SendFromPeer(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}");
            _session.Pump();
            Assert.That(_session.MatchStarted, Is.True, "重复握手后正常流程不受影响");
        }

        // -----------------------------------------------------------------
        // AC-3: 版本不匹配·非 Hello 帧 → 丢弃（权威不收到），正确版本帧正常处理
        // -----------------------------------------------------------------

        [Test]
        public void CommandWithWrongVersion_Dropped_CorrectVersionStillProcessed()
        {
            StartMatch();
            ulong versionBefore = _matchHost.StateVersion;
            int activeSeat = _controller.View.ActivePlayerId;

            // 错误版本 Command（座位正确、载荷合法）——应被版本门丢弃
            JsonValue uplink = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(new EndTurnCommand(activeSeat))));
            SendFromPeer(NetworkMessageType.Command, 999, uplink.ToJson());
            PumpAll();
            Assert.That(_matchHost.StateVersion, Is.EqualTo(versionBefore), "错误版本帧不应到达权威");
            Assert.That(_peerSide.TryReceive(out byte[]? _), Is.False, "错误版本帧不应产生任何权威回执");

            // 相同载荷、正确版本——正常处理（若非行动方则被规则拒绝但版本/回执语义仍发生）
            SendFromPeer(NetworkMessageType.Command, MessageCodec.CurrentProtocolVersion, uplink.ToJson());
            PumpAll();
            Assert.That(_session.IsConnected, Is.True, "正确版本帧处理不断链");
            Assert.That(_peerSide.TryReceive(out byte[]? _), Is.True, "正确版本帧应有权威回执（step 或拒绝）");
        }

        // -----------------------------------------------------------------
        // AC-4: 非法字段·S→C 专属类型上行 → 忽略不断链，心跳正常
        // -----------------------------------------------------------------

        [Test]
        public void ServerOnlyTypes_Uplink_Ignored_ConnectionKept()
        {
            StartMatch();

            SendFromPeer(NetworkMessageType.StateDelta, MessageCodec.CurrentProtocolVersion, "{}");
            SendFromPeer(NetworkMessageType.MatchStart, MessageCodec.CurrentProtocolVersion, "{}");
            SendFromPeer(NetworkMessageType.HelloAck, MessageCodec.CurrentProtocolVersion, "{}");
            _session.Pump();

            Assert.That(_session.IsConnected, Is.True, "S→C 类型上行不应断链");

            // 心跳仍正常：推进超过心跳阈值 → 会话发 Ping
            _clock.Advance(PeerHost.HeartbeatIntervalTicks);
            _session.Pump();
            Assert.That(_peerSide.TryReceive(out byte[]? ping) && ping != null &&
                MessageCodec.TryUnframe(ping, 0, ping.Length, out _)!.Value.Type == NetworkMessageType.Ping,
                Is.True, "注入后心跳仍应正常");
        }

        // -----------------------------------------------------------------
        // AC-5: 畸形帧·断链终态（异常不穿透 Pump）
        // -----------------------------------------------------------------

        [Test]
        public void MalformedFrame_DisposesTransport_NoThrow_ProtocolViolationLogged()
        {
            StartMatch();

            // framing 已坏：帧体长度 2 < 5（type1+version4 最小），TryUnframe 必抛 ProtocolException
            byte[] evil = { 2, 0, 0, 0, 1 };

            Assert.DoesNotThrow(() => _session.Pump(), "异常帧不得穿透 Pump 打死宿主");

            SendRawFromPeer(evil);
            Assert.DoesNotThrow(() => _session.Pump(), "异常帧不得穿透 Pump 打死宿主");
            Assert.That(_session.IsConnected, Is.False, "framing 损坏应断链（字节流无法恢复同步）");

            bool violation = false;
            foreach (NetworkEventRecord record in _diag.Export())
            {
                if (record.EventType == NetworkEventType.ProtocolViolation)
                {
                    violation = true;
                    break;
                }
            }

            Assert.That(violation, Is.True, "畸形帧应记录 ProtocolViolation 诊断事件");
        }
    }
}
