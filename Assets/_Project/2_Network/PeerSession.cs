using Card.Core;
using Card.Application.Match;

namespace Card.Network
{
    /// <summary>
    /// 房主与一个对手的会话（M12-T6 + T7）：处理 Hello 握手、Ready 状态、MatchStart 开局、心跳与超时。
    /// 生命周期：AcceptPeer 返回会话 → PeerHost 侧 Pump 驱动 → 对局运行中。
    /// 心跳基于 <see cref="IClock.CurrentTick"/>（权威 10 Hz tick）：
    /// 30 tick ≈ 3 秒无消息 → 发 Ping；60 tick ≈ 6 秒无响应 → 标记断开。
    /// （M13-T2 / M13-OBS-1：自 PeerHost.cs 拆分为独立文件，类型与行为不变。）
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
        private bool _matchActive;
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

        /// <summary>对局是否已开始（首次握手对手 Ready；或重连快照恢复后激活，M13-T7）。</summary>
        public bool MatchStarted => _handshook && (_peerReady || _matchActive);

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
            int now = _clock.CurrentTick;

            // 先处理积压消息（任何有效消息都重置活动时间）——超时判定必须在消息处理之后，
            // 否则"消息已到达但 ReceiveLoop 线程尚未入队完成"会被误判为心跳超时（M13-T8 修复）。
            if (!PumpReceivedFrames(now))
            {
                return false;
            }

            return RunHeartbeat(now);
        }

        /// <summary>处理积压上行帧：解帧（畸形即断链）→ 版本门 → 分发。返回 false 表示会话已断链。</summary>
        private bool PumpReceivedFrames(int now)
        {
            while (_transport.TryReceive(out byte[]? frame))
            {
                // 畸形帧（M13-T9）：framing 已坏 = 字节流无法恢复同步，唯一正确终态是断链
                // （掉线判负编排接管）；异常绝不能穿透 Pump 打死宿主。
                RawMessage msg;
                try
                {
                    msg = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)
                        ?? throw new ProtocolException("PeerSession 收到畸形帧。");
                }
                catch (ProtocolException ex)
                {
                    NetworkLogger.Error($"PeerSession 畸形帧断链：peerSeat={_peerSeat}，原因：{ex.Message}");
                    _diag?.Record(NetworkEventType.ProtocolViolation, ex.Message);
                    _transport.Dispose();
                    return false;
                }

                _lastActivityTick = now;

                // 版本门（M13-T9）：Hello 自带版本协商，其余上行帧版本不匹配一律丢弃（可恢复违规，不断链）
                if (msg.Type != NetworkMessageType.Hello &&
                    msg.ProtocolVersion != MessageCodec.CurrentProtocolVersion)
                {
                    NetworkLogger.Warn($"PeerSession 丢弃版本不匹配帧：type={msg.Type}，peer={msg.ProtocolVersion}，peerSeat={_peerSeat}");
                    _diag?.Record(NetworkEventType.ProtocolVersionMismatch, $"type={msg.Type}, peer={msg.ProtocolVersion}");
                    continue;
                }

                DispatchMessage(msg);
            }

            return true;
        }

        /// <summary>按消息类型分发（未知/S→C 专属类型一律记录并忽略——M13-T9 注入矩阵固化）。</summary>
        private void DispatchMessage(RawMessage msg)
        {
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
                case NetworkMessageType.RequestSnapshot:
                    HandleRequestSnapshot(msg);
                    break;
                default:
                    NetworkLogger.Warn($"PeerSession 忽略未知或方向非法的消息类型 {msg.Type}：peerSeat={_peerSeat}");
                    break;
            }
        }

        /// <summary>心跳与超时：60 tick 无任何已到达消息 → 断链；30 tick → 发 Ping。</summary>
        private bool RunHeartbeat(int now)
        {
            int idleTicks = now - _lastActivityTick;
            if (idleTicks >= PeerHost.TimeoutTicks)
            {
                _timedOut = true;
                _diag?.Record(NetworkEventType.HeartbeatTimeout, $"idle={idleTicks} ticks");
                NetworkLogger.Warn($"PeerSession 心跳超时：peerSeat={_peerSeat}, idle={idleTicks} ticks");
                _transport.Dispose();
                return false;
            }

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
            // 重复握手幂等（M13-T9）：已握手再次 Hello 不重发 HelloAck、不重复副作用
            if (_handshook)
            {
                NetworkLogger.Warn($"PeerSession 忽略重复 Hello：peerSeat={_peerSeat}");
                return;
            }

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
            // 握手门（M13-T9）：未握手的 Ready 会造成假开局，必须丢弃
            if (!_handshook)
            {
                NetworkLogger.Warn($"PeerSession 丢弃握手前 Ready：peerSeat={_peerSeat}");
                return;
            }

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

            _bridge.ForwardUplink(msg);
        }

        /// <summary>
        /// 快照请求（M13-T7）：握手后有效——重连恢复与版本冲突共用，载荷直达权威
        /// （kind=snapshot_request），权威回按座位裁剪的当前版本快照。未握手丢弃记日志。
        /// </summary>
        private void HandleRequestSnapshot(RawMessage msg)
        {
            if (!_handshook || _bridge == null)
            {
                NetworkLogger.Warn($"PeerSession 丢弃握手前 RequestSnapshot：peerSeat={_peerSeat}");
                return;
            }

            _matchActive = true; // 重连恢复即对局激活（无 Ready 二次握手），后续 Command 放行
            _diag?.Record(NetworkEventType.SnapshotRequestReceived, $"peerSeat={_peerSeat}");
            _bridge.ForwardUplink(msg);
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
