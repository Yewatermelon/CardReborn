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
        private bool _disposed;

        /// <summary>房主监听端口。</summary>
        public int ListenPort => _listener.LocalEndPoint.Port;

        /// <summary>房主玩家座位（永远 0）。</summary>
        public int HostSeat => 0;

        /// <summary>房主状态：Ready 后等待对手 Ready，双方 Ready 后自动开局。</summary>
        public bool HostReady { get; private set; }

        /// <summary>
        /// 创建房主宿主：监听指定端口（0 = OS 分配），内部持有 <see cref="MatchHost"/>。
        /// </summary>
        public PeerHost(MatchHost matchHost, int listenPort = 0)
        {
            _matchHost = matchHost;
            _listener = BclTcpTransport.Listen(listenPort);
        }

        /// <summary>标记房主 Ready。</summary>
        public void SetHostReady()
        {
            HostReady = true;
        }

        /// <summary>房主侧 Accept 一条对手连接（阻塞）。返回 PeerSession 处理握手和对局通信。</summary>
        public PeerSession? AcceptPeer()
        {
            ITransport? transport = _listener.Accept();
            if (transport == null) return null;
            return new PeerSession(_matchHost, transport, peerSeat: 1);
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
    /// 房主与一个对手的会话（M12-T6）：处理 Hello 握手、Ready 状态、MatchStart 开局。
    /// 生命周期：AcceptPeer 返回会话 → PeerHost 侧 Pump 驱动 → 对局运行中。
    /// </summary>
    public sealed class PeerSession
    {
        private readonly MatchHost _matchHost;
        private readonly ITransport _transport;
        private readonly int _peerSeat;
        private bool _handshook;
        private bool _peerReady;

        public PeerSession(MatchHost matchHost, ITransport transport, int peerSeat)
        {
            _matchHost = matchHost;
            _transport = transport;
            _peerSeat = peerSeat;
        }

        /// <summary>对手是否已 Ready。</summary>
        public bool PeerReady => _peerReady;

        /// <summary>握手是否完成（Hello ↔ HelloAck 已交换）。</summary>
        public bool Handshook => _handshook;

        /// <summary>
        /// Pump 驱动：读取对手上行消息，按握手阶段处理。
        /// 返回 true 表示对局已开局，后续应走 MatchHost.Pump + PeerHost 广播流程（M13）。
        /// </summary>
        public bool Pump()
        {
            while (_transport.TryReceive(out byte[]? frame))
            {
                RawMessage msg = MessageCodec.TryUnframe(frame, 0, frame.Length, out _)
                    ?? throw new ProtocolException("PeerSession 收到畸形帧。");

                switch (msg.Type)
                {
                    case NetworkMessageType.Hello:
                        HandleHello(msg);
                        break;
                    case NetworkMessageType.Ready:
                        HandleReady();
                        break;
                    case NetworkMessageType.Leave:
                        _transport.Dispose();
                        break;
                    default:
                        // Command 等对局消息——暂不处理，M13 接入 MatchHost 上行
                        break;
                }
            }

            return false;
        }

        private void HandleHello(RawMessage msg)
        {
            int peerVersion = msg.ProtocolVersion;

            if (peerVersion != MessageCodec.CurrentProtocolVersion)
            {
                // 版本不匹配——发 Error 后断开
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
        }

        private void HandleReady()
        {
            _peerReady = true;
            // 双方 Ready——发 MatchStart（M13 接入对局正式开局）
            JsonValue start = JsonValue.Object()
                .Add("protocolVersion", JsonValue.From(MessageCodec.CurrentProtocolVersion))
                .Add("hostSeat", JsonValue.From(0))
                .Add("peerSeat", JsonValue.From(_peerSeat));
            _transport.Send(MessageCodec.Frame(NetworkMessageType.MatchStart, MessageCodec.CurrentProtocolVersion, start.ToJson()));
        }

        private void SendError(string code)
        {
            JsonValue err = JsonValue.Object()
                .Add("code", JsonValue.From(code));
            _transport.Send(MessageCodec.Frame(NetworkMessageType.Error, MessageCodec.CurrentProtocolVersion, err.ToJson()));
        }
    }
}
