using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Network;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>
    /// 座位传输适配（M14-T3）：把"下行快照/增量 JSON + 上行命令信封"统一为
    /// 一个接口，供 <see cref="NetMatchDriver"/> 与战斗装配消费——房主（回环）
    /// 与加入方（TCP）走同一条客户端管线（铁律 12：客户端只收状态、只上行命令）。
    /// </summary>
    internal interface INetMatchLink : IDisposable
    {
        /// <summary>链路是否存活（未 Dispose 且传输未断）。</summary>
        bool Alive { get; }

        /// <summary>对局是否已开局（房主=对手 Ready；加入方=收到 MatchStart）。</summary>
        bool MatchStarted { get; }

        /// <summary>驱动传输泵；经回调交出下行消息（kind=snapshot/step）。</summary>
        void Pump(Action<JsonValue> onDownlink);

        /// <summary>上行一条命令信封 JSON（{ kind:"command", payload, seq }）。</summary>
        void SendUplink(string envelopeJson);

        /// <summary>准备上行（房主=SetHostReady 流程引导；加入方=SendReady 触发开局）。</summary>
        void SendReady();

        /// <summary>对局终态通知（加入方：MatchEnd 帧；房主侧不产生——终局经 step.finished）。</summary>
        event Action<int?, string>? MatchEnded;
    }

    /// <summary>
    /// 房主座位链路：包装权威 <see cref="MatchHost"/> + <see cref="PeerHost"/>。
    /// 每次泵：非阻塞接管待接受连接 → 网络权威 tick → 排空房主本地座位 0 回环下行。
    /// </summary>
    internal sealed class HostNetLink : INetMatchLink
    {
        private readonly MatchHost _matchHost;
        private readonly PeerHost _peerHost;
        private bool _disposed;

        public HostNetLink(MatchHost matchHost, PeerHost peerHost)
        {
            _matchHost = Guard.NotNull(matchHost, nameof(matchHost));
            _peerHost = Guard.NotNull(peerHost, nameof(peerHost));
        }

        /// <summary>底层 PeerHost（房主 Ready / 监听地址等流程信息）。</summary>
        public PeerHost PeerHost => _peerHost;

        public bool Alive => !_disposed;

        public bool MatchStarted => _peerHost.MatchLive;

        public event Action<int?, string>? MatchEnded;

        public void Pump(Action<JsonValue> onDownlink)
        {
            if (_disposed)
            {
                return;
            }

            _peerHost.TryAcceptPeer();
            _peerHost.RunNetworkTick();

            // 房主本地座位：权威直气回环下行（与对手走同一份 step 内容，M13-T1）
            LoopbackEndpoint local = _peerHost.LocalClientEndpoint;
            while (local.TryReceive(out string raw))
            {
                onDownlink(JsonValue.Parse(raw));
            }
        }

        public void SendUplink(string envelopeJson)
        {
            if (_disposed)
            {
                NetworkLogger.Warn("HostNetLink 已释放，丢弃上行信封。");
                return;
            }

            _peerHost.LocalClientEndpoint.Send(envelopeJson);
        }

        /// <summary>房主准备 = SetHostReady（M12 语义：不门控开局，仅流程引导）。</summary>
        public void SendReady()
        {
            _peerHost.SetHostReady();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _peerHost.Dispose();
        }
    }

    /// <summary>加入方座位链路：包装 <see cref="NetworkMatchClient"/>（BCL TCP）。</summary>
    internal sealed class JoinNetLink : INetMatchLink
    {
        private readonly NetworkMatchClient _client;
        private readonly Queue<JsonValue> _downlink = new Queue<JsonValue>();
        private bool _disposed;

        public JoinNetLink(NetworkMatchClient client)
        {
            _client = Guard.NotNull(client, nameof(client));
            _client.SnapshotReceived += json => _downlink.Enqueue(json);
            _client.StepReceived += json => _downlink.Enqueue(json);
            _client.MatchEnded += (winner, reason) => MatchEnded?.Invoke(winner, reason);
        }

        public bool Alive => !_disposed && _client.Connected;

        public bool MatchStarted => _client.MatchStarted;

        public event Action<int?, string>? MatchEnded;

        public void Pump(Action<JsonValue> onDownlink)
        {
            if (_disposed)
            {
                return;
            }

            _client.Pump();
            while (_downlink.Count > 0)
            {
                onDownlink(_downlink.Dequeue());
            }
        }

        public void SendUplink(string envelopeJson)
        {
            if (_disposed)
            {
                NetworkLogger.Warn("JoinNetLink 已释放，丢弃上行信封。");
                return;
            }

            _client.SendCommand(envelopeJson);
        }

        /// <summary>加入方准备 = SendReady（双方 Ready 后宿主开局，M12 语义）。</summary>
        public void SendReady()
        {
            _client.SendReady();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.Dispose();
        }
    }
}
