using System;
using Card.Core;
using Card.Application.Match;

namespace Card.Network
{
    /// <summary>
    /// 房主模式宿主（M12-T6；Docs/05 §2.4）：包装权威 <see cref="MatchHost"/> + BCL TCP 监听。
    /// 同一进程内可同时担任玩家 1（房主）和权威运行时——通过 <see cref="CreatePair"/> 生成
    /// 两个端点：一个给 PeerHost（权威侧），一个给房主客户端进程内 Loopback。
    ///
    /// 握手流程（Docs/05 §9.2）：Hello → HelloAck → Ready → MatchStart。
    /// 本阶段不做：多房间、匹配、账号、中继、NAT 穿透。
    /// （M13-T2 / M13-OBS-1：PeerSession 已拆分至独立文件 PeerSession.cs。）
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

}
