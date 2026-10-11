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

        // 终态编排状态（M13-T8）
        private bool _matchLive;
        private ulong _lastSeenVersion;
        private int _turnStartTick;
        private int? _disconnectStartTick;
        private string? _pendingEndReason;
        private bool _matchEndSent;

        /// <summary>心跳间隔（权威 tick，10 Hz — 30 tick ≈ 3 秒）。</summary>
        public const int HeartbeatIntervalTicks = 30;

        /// <summary>超时阈值（权威 tick — 60 tick ≈ 6 秒无响应判定断开）。</summary>
        public const int TimeoutTicks = 60;

        /// <summary>回合操作超时（权威 tick — 750 tick ≈ 75 秒无权威状态变更代发结束回合，Docs/05 §8.2）。</summary>
        public const int TurnTimeoutTicks = 750;

        /// <summary>掉线重连窗口（权威 tick — 300 tick ≈ 30 秒，超时判负）。</summary>
        public const int ReconnectWindowTicks = 300;

        /// <summary>MatchEnd 结束原因：正常终局。</summary>
        public const string EndReasonNormal = "Normal";

        /// <summary>MatchEnd 结束原因：掉线判负。</summary>
        public const string EndReasonDisconnect = "Disconnect";

        /// <summary>MatchEnd 结束原因：房主退出作废。</summary>
        public const string EndReasonHostQuit = "HostQuit";

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

        /// <summary>当前对手会话（AcceptPeer 后存在；M13-T8）。</summary>
        public PeerSession? ActiveSession => _session;

        /// <summary>对局是否已激活（粘性：任一会话报告开局后保持 true，M13-T8）。</summary>
        public bool MatchLive => _matchLive;

        /// <summary>最近一次 MatchEnd 的结束原因（正常终局前为 null；M13-T8）。</summary>
        public string? LastMatchEndReason => _pendingEndReason;

        /// <summary>
        /// 创建房主宿主：监听指定端口（0 = OS 分配），内部持有 <see cref="MatchHost"/>；
        /// 同时把房主本地座位 0 的回环端点挂到权威。
        /// </summary>
        public PeerHost(MatchHost matchHost, IClock clock, int listenPort = 0)
        {
            _matchHost = matchHost;
            _clock = clock;
            _turnStartTick = clock.CurrentTick;
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
        /// 重连接管（M13-T7）：已有旧会话时先从权威摘除旧桥接端点（旧连接不再收广播），
        /// 同一座位以新会话重建——重连恢复由握手后的 RequestSnapshot 驱动。
        /// </summary>
        public PeerSession? AcceptPeer()
        {
            ITransport? transport = _listener.Accept();
            if (transport == null) return null;

            if (_bridge != null)
            {
                _matchHost.Detach(_bridge.KernelEndpoint);
                NetworkLogger.Info($"PeerHost 重连接管：旧座位 {_bridge.Seat} 端点已摘除");
            }

            LoopbackLink.CreatePair(out LoopbackEndpoint peerKernelEp, out LoopbackEndpoint peerBridgeEp);
            _matchHost.Attach(peerKernelEp, 1);
            _bridge = new HostBridge(peerBridgeEp, transport, seat: 1);
            _session = new PeerSession(_matchHost, transport, _clock, peerSeat: 1, bridge: _bridge);
            _disconnectStartTick = null; // 重连即取消掉线判负计时（M13-T8）
            return _session;
        }

        /// <summary>
        /// 网络权威 tick（M13-T1）：会话泵（握手/心跳/命令转发）→ 权威推进 → 桥接下行
        /// → 终态编排（M13-T8：回合超时 / 掉线判负 / MatchEnd 下发）。
        /// 由宿主侧按固定节奏驱动（Docs/05 §4.3）。
        /// </summary>
        public void RunNetworkTick()
        {
            _session?.Pump();
            _matchHost.Pump();
            _bridge?.PumpKernelToNetwork();
            RunTerminalOrchestration();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // 房主退出终态（M13-T8）：对局进行中尽力通知对手作废后关闭监听
            if (_matchLive && !_matchHost.IsFinished)
            {
                NetworkLogger.Info("PeerHost 房主退出：对局作废，尽力通知对手");
                _bridge?.SendMatchEnd(null, EndReasonHostQuit);
            }

            _listener.Dispose();
        }

        // -----------------------------------------------------------------
        // 私有：终态编排（M13-T8，Docs/05 §8.2 / §8.3——不无限等待、不假活）
        // -----------------------------------------------------------------

        private void RunTerminalOrchestration()
        {
            // 对局激活跟踪（粘性）：会话报告 MatchStarted（首次 Ready 或重连恢复）即视为对局中
            if (_session != null && _session.MatchStarted)
            {
                _matchLive = true;
            }

            int now = _clock.CurrentTick;

            // 版本推进 → 回合计时重置（口径：75s 内无任何权威状态变更即代发）
            if (_matchHost.StateVersion != _lastSeenVersion)
            {
                _lastSeenVersion = _matchHost.StateVersion;
                _turnStartTick = now;
            }

            RunTurnTimeout(now);
            RunDisconnectForfeit(now);
            SendMatchEndIfDecided();
        }

        /// <summary>回合操作超时：对局进行中 75s 无权威状态变更 → 代发结束当前回合。</summary>
        private void RunTurnTimeout(int now)
        {
            if (!_matchLive || _matchHost.IsFinished ||
                now - _turnStartTick < TurnTimeoutTicks)
            {
                return;
            }

            int seat = _matchHost.ActiveSeat;
            NetworkLogger.Warn($"PeerHost 回合操作超时：idle={now - _turnStartTick} ticks，代发结束回合 seat={seat}");
            _matchHost.SubmitAuthority(new EndTurnCommand(seat));
            // 同步版本基线与计时：代发即视为一次权威状态变更，下轮不重复触发
            _lastSeenVersion = _matchHost.StateVersion;
            _turnStartTick = _clock.CurrentTick;
        }

        /// <summary>掉线判负：对局进行中掉线超过重连窗口 → 代发对手判负；重连即取消。</summary>
        private void RunDisconnectForfeit(int now)
        {
            if (!_matchLive || _matchHost.IsFinished ||
                _session == null || _session.IsConnected)
            {
                _disconnectStartTick = null;
                return;
            }

            _disconnectStartTick ??= now;
            if (now - _disconnectStartTick.Value < ReconnectWindowTicks)
            {
                return;
            }

            int forfeitSeat = _bridge?.Seat ?? 1;
            NetworkLogger.Warn($"PeerHost 重连窗口超时：掉线 {now - _disconnectStartTick.Value} ticks，seat={forfeitSeat} 判负");
            _pendingEndReason = EndReasonDisconnect;
            _matchHost.SubmitAuthority(new ForfeitCommand(forfeitSeat));
            _disconnectStartTick = null;
        }

        /// <summary>对局终态一次性下发：MatchEnd { winner, reason } 给对手。</summary>
        private void SendMatchEndIfDecided()
        {
            if (!_matchLive || _matchEndSent || !_matchHost.IsFinished)
            {
                return;
            }

            _matchEndSent = true;
            _pendingEndReason ??= EndReasonNormal;
            int? winner = _matchHost.Outcome?.WinnerId;
            NetworkLogger.Info($"PeerHost 对局结束：winner={winner?.ToString() ?? "null"}，reason={_pendingEndReason}");
            _bridge?.SendMatchEnd(winner, _pendingEndReason);
        }
    }
}
