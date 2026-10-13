using System;
using System.Diagnostics;
using Card.Core;

namespace Card.Network
{
    /// <summary>
    /// 加入方客户端会话（M14-T3，Docs/05 §3.4 NetworkMatchClient 补齐）：
    /// 生命周期 = 连接（外部建 <see cref="ITransport"/>）→ <see cref="SendHello"/> →
    /// HelloAck → <see cref="SendReady"/> → MatchStart → 下行分发（快照 / 增量 / 终态）→ 断开。
    /// 只做帧分类与事件分发，不持视图状态——快照/增量应用归
    /// <c>ClientStateSync</c>（Card.Application），组合归 Bootstrap（铁律 12）。
    /// 防御姿态与 PeerSession 对称（M13-T9 口径）：畸形帧 / 载荷非法 JSON → 断链；
    /// 帧级版本不匹配只丢弃不断链（§9.3 只定义握手版本协商）；未知帧类型忽略记日志。
    /// </summary>
    public sealed class NetworkMatchClient : IDisposable
    {
        private readonly ITransport _transport;
        private readonly NetworkDiagnostics? _diag;
        private bool _handshook;
        private bool _matchStarted;
        private bool _disconnectRaised;
        private bool _disposed;

        /// <summary>构造客户端会话；传输由调用方建立（BclTcpTransport.Connect）。</summary>
        public NetworkMatchClient(ITransport transport, NetworkDiagnostics? diag = null)
        {
            _transport = Guard.NotNull(transport, nameof(transport));
            _diag = diag;
            _transport.OnDisconnected += () => _diag?.Record(NetworkEventType.Disconnected, "transport-closed");
        }

        /// <summary>传输层是否仍连接（Dispose 后恒 false）。</summary>
        public bool Connected => !_disposed && _transport.Connected;

        /// <summary>底层传输（Soak/诊断用；正常业务不应直用——帧分类归本类）。</summary>
        public ITransport Transport => _transport;

        /// <summary>握手是否完成（已收 HelloAck）。</summary>
        public bool Handshook => _handshook;

        /// <summary>对局是否已开始（已收 MatchStart）。</summary>
        public bool MatchStarted => _matchStarted;

        /// <summary>收到 HelloAck（载荷：协议版本确认 + 座位）。</summary>
        public event Action<JsonValue>? HelloAckReceived;

        /// <summary>收到 MatchStart（载荷：对局开始信息）。</summary>
        public event Action<JsonValue>? MatchStartReceived;

        /// <summary>收到开局/重连快照（{ kind:"snapshot", version, state }）。</summary>
        public event Action<JsonValue>? SnapshotReceived;

        /// <summary>收到权威增量 step（StateDelta）。</summary>
        public event Action<JsonValue>? StepReceived;

        /// <summary>收到对局终态；winner 为 null 表示对局作废（房主退出）。</summary>
        public event Action<int?, string>? MatchEnded;

        /// <summary>收到宿主错误帧（错误码）。</summary>
        public event Action<string>? ErrorReceived;

        /// <summary>断链（传输关闭 / 畸形帧 / 载荷非法）；至多触发一次。</summary>
        public event Action<string>? Disconnected;

        /// <summary>泵驱动：排空下行帧并分发；传输已断开时触发 Disconnected。</summary>
        public void Pump()
        {
            if (_disposed || _disconnectRaised)
            {
                return;
            }

            while (_transport.TryReceive(out byte[]? frame))
            {
                if (!DispatchFrame(frame!))
                {
                    return; // 已断链，不再消费
                }
            }

            if (!_transport.Connected)
            {
                Disconnect("连接已关闭。");
            }
        }

        /// <summary>发送握手 Hello（携带当前协议版本与玩家名）。</summary>
        public void SendHello(string playerName)
        {
            JsonValue payload = JsonValue.Object().Add("playerName", JsonValue.From(playerName));
            SendFrame(NetworkMessageType.Hello, payload.ToJson());
        }

        /// <summary>发送准备帧。</summary>
        public void SendReady()
        {
            SendFrame(NetworkMessageType.Ready, "{}");
        }

        /// <summary>发送命令信封（{ kind:"command", payload, seq }——信封原文透传）。</summary>
        public void SendCommand(string envelopeJson)
        {
            SendFrame(NetworkMessageType.Command, envelopeJson);
        }

        /// <summary>关闭会话（调用方主动断开，不触发 Disconnected 事件）。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _transport.Dispose();
        }

        // -----------------------------------------------------------------
        // 私有：帧分发
        // -----------------------------------------------------------------

        /// <summary>分发一帧；返回 false 表示已断链，调用方立即停止消费。</summary>
        private bool DispatchFrame(byte[] frame)
        {
            RawMessage msg;
            try
            {
                msg = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)
                    ?? throw new ProtocolException("NetworkMatchClient 收到畸形帧。");
            }
            catch (ProtocolException ex)
            {
                Disconnect("畸形帧：" + ex.Message);
                return false;
            }

            if (msg.ProtocolVersion != MessageCodec.CurrentProtocolVersion)
            {
                NetworkLogger.Warn($"NetworkMatchClient 丢弃版本不匹配帧：type={msg.Type}，peer={msg.ProtocolVersion}");
                _diag?.Record(NetworkEventType.ProtocolVersionMismatch, $"type={msg.Type}, peer={msg.ProtocolVersion}");
                return true;
            }

            JsonValue payload;
            long parseStart = Stopwatch.GetTimestamp();
            try
            {
                payload = JsonValue.Parse(msg.PayloadJson);
            }
            catch (FormatException ex)
            {
                Disconnect("载荷不是合法 JSON：" + ex.Message);
                return false;
            }

            if (msg.Type == NetworkMessageType.StateDelta)
            {
                double parseMs = (double)(Stopwatch.GetTimestamp() - parseStart) / Stopwatch.Frequency * 1000;
                _diag?.Record(NetworkEventType.ReceiveFrame,
                    $"delta json={msg.PayloadJson.Length} parse={parseMs:F1}ms");
            }

            return DispatchByType(msg.Type, payload);
        }

        private bool DispatchByType(NetworkMessageType type, JsonValue payload)
        {
            switch (type)
            {
                case NetworkMessageType.HelloAck:
                    _handshook = true;
                    _diag?.Record(NetworkEventType.HelloAckSent, "client");
                    HelloAckReceived?.Invoke(payload);
                    break;
                case NetworkMessageType.MatchStart:
                    _matchStarted = true;
                    MatchStartReceived?.Invoke(payload);
                    break;
                case NetworkMessageType.MatchSnapshot:
                    SnapshotReceived?.Invoke(payload);
                    break;
                case NetworkMessageType.StateDelta:
                    StepReceived?.Invoke(payload);
                    break;
                case NetworkMessageType.MatchEnd:
                    MatchEnded?.Invoke(ReadWinner(payload), ReadReason(payload));
                    break;
                case NetworkMessageType.Error:
                    ErrorReceived?.Invoke(ReadCode(payload));
                    break;
                case NetworkMessageType.Ping:
                    ReplyPong(payload);
                    break;
                case NetworkMessageType.Pong:
                    _diag?.Record(NetworkEventType.PongReceived);
                    break;
                default:
                    NetworkLogger.Warn($"NetworkMatchClient 忽略未知或方向非法的消息类型 {type}");
                    break;
            }

            return true;
        }

        /// <summary>宿主 Ping → 自动回 Pong（回显时间戳成员）。</summary>
        private void ReplyPong(JsonValue pingPayload)
        {
            _diag?.Record(NetworkEventType.PingReceived);
            JsonValue pong = JsonValue.Object();
            if (pingPayload.TryGetMember("timestamp", out JsonValue timestamp))
            {
                pong = pong.Add("timestamp", timestamp);
            }

            SendFrame(NetworkMessageType.Pong, pong.ToJson());
        }

        private void SendFrame(NetworkMessageType type, string payloadJson)
        {
            if (_disposed || _disconnectRaised || !_transport.Connected)
            {
                NetworkLogger.Warn($"NetworkMatchClient 传输不可用，丢弃上行帧：type={type}");
                return;
            }

            _transport.Send(MessageCodec.Frame(type, MessageCodec.CurrentProtocolVersion, payloadJson));
            _diag?.Record(NetworkEventType.SendFrame, $"type={type}");
        }

        private void Disconnect(string reason)
        {
            if (_disconnectRaised || _disposed)
            {
                return;
            }

            _disconnectRaised = true;
            _diag?.Record(NetworkEventType.Disconnected, reason);
            NetworkLogger.Warn("NetworkMatchClient 断链：" + reason);
            _transport.Dispose();
            Disconnected?.Invoke(reason);
        }

        private static int? ReadWinner(JsonValue payload)
        {
            return payload.TryGetMember("winner", out JsonValue winner)
                && winner.Kind != JsonKind.Null
                && winner.Kind == JsonKind.Number
                ? winner.IntValue
                : null;
        }

        private static string ReadReason(JsonValue payload)
        {
            return payload.TryGetMember("reason", out JsonValue reason) ? reason.StringValue : string.Empty;
        }

        private static string ReadCode(JsonValue payload)
        {
            return payload.TryGetMember("code", out JsonValue code) ? code.StringValue : string.Empty;
        }
    }
}
