using System;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Match;

namespace Card.Network
{
    /// <summary>
    /// 房主模式宿主（M12-T6；Docs/05 §2.4）：包装权威 <see cref="MatchHost"/> + BCL TCP 监听。
    /// 同一进程内可同时担任玩家 1（房主）和权威运行时——通过 <see cref="CreatePair"/> 生成
    /// 两个端点：一个给 PeerHost（权威侧），一个给房主客户端进程内 Loopback。
    ///
    /// 握手流程（Docs/05 §9.2）：Hello → HelloAck → Ready → MatchStart。
    /// 本阶段不做：多房间、匹配、账号、中继、NAT 穿透。
    /// </summary>
    public sealed class PeerHost : IDisposable
    {
        private readonly MatchHost _matchHost;
        private readonly BclTcpHostListener _listener;
        private readonly IClock _clock;
        private readonly LoopbackEndpoint _localClientEndpoint;
        private PeerSession? _session;
        private HostBridge? _bridge;
        private bool _disposed;

        /// <summary>心跳间隔（权威 tick，10 Hz — 30 tick ≈ 3 秒）。</summary>
        public const int HeartbeatIntervalTicks = 30;

        /// <summary>超时阈值（权威 tick — 60 tick ≈ 6 秒无响应判定断开）。</summary>
        public const int TimeoutTicks = 60;

        /// <summary>房主监听端口。</summary>
        public int ListenPort => _listener.LocalEndPoint.Port;

        /// <summary>房主玩家座位（永远 0）。</summary>
        public int HostSeat => 0;

        /// <summary>房主状态：Ready 后等待对手 Ready，双方 Ready 后自动开局。</summary>
        public bool HostReady { get; private set; }

        /// <summary>房主本地客户端端点（座位 0）：房主 UI 发命令/收快照走此回环（M13-T1）。</summary>
        public LoopbackEndpoint LocalClientEndpoint => _localClientEndpoint;

        /// <summary>当前对手桥接器（AcceptPeer 后存在；M13-T1）。</summary>
        public HostBridge? ActiveBridge => _bridge;

        /// <summary>
        /// 创建房主宿主：监听指定端口（0 = OS 分配），内部持有 <see cref="MatchHost"/>；
        /// 同时把房主本地座位 0 的回环端点挂到权威。
        /// </summary>
        public PeerHost(MatchHost matchHost, IClock clock, int listenPort = 0)
        {
            _matchHost = matchHost;
            _clock = clock;
            LoopbackLink.CreatePair(out LoopbackEndpoint hostKernelEp, out _localClientEndpoint);
            matchHost.Attach(hostKernelEp, HostSeat);
            _listener = BclTcpTransport.Listen(listenPort);
        }

        /// <summary>标记房主 Ready。</summary>
        public void SetHostReady()
        {
            HostReady = true;
        }

        /// <summary>
        /// 房主侧 Accept 一条对手连接（阻塞）。返回 PeerSession；同时建立座位 1 的
        /// 桥接器（网络帧 ↔ 权威上行/下行，M13-T1）。
        /// </summary>
        public PeerSession? AcceptPeer()
        {
            ITransport? transport = _listener.Accept();
            if (transport == null) return null;

            LoopbackLink.CreatePair(out LoopbackEndpoint peerKernelEp, out LoopbackEndpoint peerBridgeEp);
            _matchHost.Attach(peerKernelEp, 1);
            _bridge = new HostBridge(peerBridgeEp, transport, seat: 1);
            _session = new PeerSession(_matchHost, transport, _clock, peerSeat: 1, bridge: _bridge);
            return _session;
        }

        /// <summary>
        /// 网络权威 tick（M13-T1）：会话泵（握手/心跳/命令转发）→ 权威推进 → 桥接下行。
        /// 由宿主侧按固定节奏驱动（Docs/05 §4.3）。
        /// </summary>
        public void RunNetworkTick()
        {
            _session?.Pump();
            _matchHost.Pump();
            _bridge?.PumpKernelToNetwork();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _listener.Dispose();
            }
        }
    }

    /// <summary>
    /// 房主与一个对手的会话（M12-T6 + T7）：处理 Hello 握手、Ready 状态、MatchStart 开局、心跳与超时。
    /// 生命周期：AcceptPeer 返回会话 → PeerHost 侧 Pump 驱动 → 对局运行中。
    /// 心跳基于 <see cref="IClock.CurrentTick"/>（权威 10 Hz tick）：
    /// 30 tick ≈ 3 秒无消息 → 发 Ping；60 tick ≈ 6 秒无响应 → 标记断开。
    /// </summary>
    public sealed class PeerSession
    {
        private readonly MatchHost _matchHost;
        private readonly ITransport _transport;
        private readonly IClock _clock;
        private readonly int _peerSeat;
        private readonly HostBridge? _bridge;
        private readonly NetworkDiagnostics? _diag;
        private bool _handshook;
        private bool _peerReady;
        private int _lastActivityTick;
        private bool _timedOut;

        public PeerSession(MatchHost matchHost, ITransport transport, IClock clock, int peerSeat,
            HostBridge? bridge = null, NetworkDiagnostics? diag = null)
        {
            _matchHost = matchHost;
            _transport = transport;
            _clock = clock;
            _peerSeat = peerSeat;
            _bridge = bridge;
            _diag = diag;
            _lastActivityTick = clock.CurrentTick;
        }

        /// <summary>对手是否已 Ready。</summary>
        public bool PeerReady => _peerReady;

        /// <summary>握手是否完成（Hello ↔ HelloAck 已交换）。</summary>
        public bool Handshook => _handshook;

        /// <summary>对局是否已开始（握手完成且对手 Ready；M13-T1 起 Command 帧仅在此后转发）。</summary>
        public bool MatchStarted => _handshook && _peerReady;

        /// <summary>是否因超时判定断开。</summary>
        public bool TimedOut => _timedOut;

        /// <summary>传输层是否仍连接。</summary>
        public bool IsConnected => _transport.Connected && !_timedOut;

        /// <summary>
        /// Pump 驱动：读取对手上行消息，按握手阶段处理，执行心跳与超时检测。
        /// 返回 true 表示对局已开局，后续应走 MatchHost.Pump + PeerHost 广播流程（M13）。
        /// </summary>
        public bool Pump()
        {
            // 心跳超时检测
            int now = _clock.CurrentTick;
            int idleTicks = now - _lastActivityTick;

            if (idleTicks >= PeerHost.TimeoutTicks)
            {
                _timedOut = true;
                _diag?.Record(NetworkEventType.HeartbeatTimeout, $"idle={idleTicks} ticks");
                NetworkLogger.Warn($"PeerSession 心跳超时：peerSeat={_peerSeat}, idle={idleTicks} ticks");
                _transport.Dispose();
                return false;
            }

            while (_transport.TryReceive(out byte[]? frame))
            {
                RawMessage msg = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)
                    ?? throw new ProtocolException("PeerSession 收到畸形帧。");

                _lastActivityTick = now; // 任何有效消息都重置活动时间

                switch (msg.Type)
                {
                    case NetworkMessageType.Hello:
                        HandleHello(msg);
                        break;
                    case NetworkMessageType.Ready:
                        HandleReady();
                        break;
                    case NetworkMessageType.Leave:
                        NetworkLogger.Info($"PeerSession 对手主动离开：peerSeat={_peerSeat}");
                        _diag?.Record(NetworkEventType.Disconnected, "peer sent Leave");
                        _transport.Dispose();
                        break;
                    case NetworkMessageType.Ping:
                        HandlePing(msg);
                        break;
                    case NetworkMessageType.Pong:
                        HandlePong();
                        _diag?.Record(NetworkEventType.PongReceived);
                        break;
                    case NetworkMessageType.Command:
                        HandleCommand(msg);
                        break;
                    default:
                        // 未知上行类型——记录并忽略（T9 完善异常注入矩阵）
                        NetworkLogger.Warn($"PeerSession 收到未知消息类型 {msg.Type}：peerSeat={_peerSeat}");
                        break;
                }
            }

            // 发心跳 Ping
            if (idleTicks >= PeerHost.HeartbeatIntervalTicks && _handshook)
            {
                JsonValue ping = JsonValue.Object()
                    .Add("timestamp", JsonValue.From(now));
                _transport.Send(MessageCodec.Frame(
                    NetworkMessageType.Ping,
                    MessageCodec.CurrentProtocolVersion,
                    ping.ToJson()));
                _diag?.Record(NetworkEventType.PingSent, $"tick={now}");
            }

            return false;
        }

        private void HandleHello(RawMessage msg)
        {
            int peerVersion = msg.ProtocolVersion;
            _diag?.Record(NetworkEventType.HelloReceived, $"peerVersion={peerVersion}");

            if (peerVersion != MessageCodec.CurrentProtocolVersion)
            {
                // 版本不匹配——发 Error 后断开
                _diag?.Record(NetworkEventType.ProtocolVersionMismatch, $"peer={peerVersion}, current={MessageCodec.CurrentProtocolVersion}");
                NetworkLogger.Warn($"PeerSession 协议版本不匹配：peer={peerVersion}, current={MessageCodec.CurrentProtocolVersion}");
                SendError("ProtocolVersionMismatch");
                _transport.Dispose();
                return;
            }

            // 发 HelloAck
            JsonValue ack = JsonValue.Object()
                .Add("protocolVersion", JsonValue.From(MessageCodec.CurrentProtocolVersion))
                .Add("hostSeat", JsonValue.From(0))
                .Add("peerSeat", JsonValue.From(_peerSeat));
            _transport.Send(MessageCodec.Frame(NetworkMessageType.HelloAck, MessageCodec.CurrentProtocolVersion, ack.ToJson()));
            _handshook = true;
            _diag?.Record(NetworkEventType.HelloAckSent);
            NetworkLogger.Info($"PeerSession 握手完成：peerSeat={_peerSeat}");
        }

        private void HandleReady()
        {
            _peerReady = true;
            _diag?.Record(NetworkEventType.ReadyReceived);
            // 双方 Ready——发 MatchStart（M13 接入对局正式开局）
            JsonValue start = JsonValue.Object()
                .Add("protocolVersion", JsonValue.From(MessageCodec.CurrentProtocolVersion))
                .Add("hostSeat", JsonValue.From(0))
                .Add("peerSeat", JsonValue.From(_peerSeat));
            _transport.Send(MessageCodec.Frame(NetworkMessageType.MatchStart, MessageCodec.CurrentProtocolVersion, start.ToJson()));
            _diag?.Record(NetworkEventType.MatchStartSent);
            // M13-T1：开局快照——桥接器向权威注入 hello，权威 Pump 后回按座位投影的快照
            _bridge?.NotifyMatchStarted();
            NetworkLogger.Info($"PeerSession 对局开始：peerSeat={_peerSeat}");
        }

        private void HandleCommand(RawMessage msg)
        {
            if (!MatchStarted || _bridge == null)
            {
                NetworkLogger.Warn($"PeerSession 丢弃开局前 Command 帧：peerSeat={_peerSeat}");
                return;
            }

            _bridge.ForwardCommand(msg);
        }

        private void HandlePing(RawMessage msg)
        {
            // 对手发 Ping——立即回 Pong（带原始时间戳用于 RTT 计算）
            _diag?.Record(NetworkEventType.PingReceived);
            JsonValue pong = JsonValue.Object()
                .Add("timestamp", JsonValue.From(msg.PayloadJson)); // 原样回传
            _transport.Send(MessageCodec.Frame(
                NetworkMessageType.Pong,
                MessageCodec.CurrentProtocolVersion,
                pong.ToJson()));
            _diag?.Record(NetworkEventType.PongSent);
        }

        private void HandlePong()
        {
            // 只重置活动时间戳（已在 Pump 循环开头处理）
        }

        private void SendError(string code)
        {
            JsonValue err = JsonValue.Object()
                .Add("code", JsonValue.From(code));
            _transport.Send(MessageCodec.Frame(NetworkMessageType.Error, MessageCodec.CurrentProtocolVersion, err.ToJson()));
        }
    }
}
