using System;
using System.Globalization;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Network;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>
    /// 联机大厅状态机（M14-T3）：房主——创建对局（权威 + TCP 监听 7777）→ 准备（流程引导）→
    /// 等对手；加入方——连接房主地址 → 准备 → 等开局。开局（MatchStarted + 快照基线就绪）后
    /// 经 <see cref="Driver"/> 交付 <see cref="NetMatchDriver"/>。失败（端口占用 / 地址非法 /
    /// 连接拒绝 / 断链）经 <see cref="StatusChanged"/>（"!" 前缀错误文本）反馈。
    /// 测试注入：internal 构造可替换 host/join 链路工厂（fake link 驱动状态机）。
    /// </summary>
    internal sealed class PvpLobbyFlow
    {
        /// <summary>房主默认监听端口（"再来一局"重载后地址可复现）。</summary>
        public const int DefaultListenPort = 7777;

        private const string HostAddressPrefix = "127.0.0.1:";

        private readonly CardDatabase _database;
        private readonly int _listenPort;
        private readonly IClock _clock;
        private readonly Func<INetMatchLink>? _hostFactoryOverride;
        private readonly Func<string, INetMatchLink>? _joinFactoryOverride;

        private INetMatchLink? _link;
        private NetMatchDriver? _driver;
        private bool _isHosting;
        private bool _disconnectReported;

        /// <param name="database">卡牌配置（开局建库）。</param>
        /// <param name="listenPort">房主监听端口。</param>
        /// <param name="clock">权威宿主逻辑时钟（Unity 侧注入 UnityTickClock）。</param>
        public PvpLobbyFlow(CardDatabase database, int listenPort, IClock clock)
            : this(database, listenPort, clock, null, null)
        {
        }

        internal PvpLobbyFlow(CardDatabase database, int listenPort, IClock clock,
            Func<INetMatchLink>? hostFactory, Func<string, INetMatchLink>? joinFactory)
        {
            _database = Guard.NotNull(database, nameof(database));
            _listenPort = listenPort;
            _clock = Guard.NotNull(clock, nameof(clock));
            _hostFactoryOverride = hostFactory;
            _joinFactoryOverride = joinFactory;
        }

        /// <summary>本流程是否为房主（false = 加入方或未创建）。</summary>
        public bool IsHosting => _isHosting;

        /// <summary>房主监听地址（"127.0.0.1:port"；未创建为 null）。</summary>
        public string? ListenAddress { get; private set; }

        /// <summary>已交付的驱动（创建/连接成功后存在；失败为 null）。</summary>
        public NetMatchDriver? Driver => _driver;

        /// <summary>对局是否就绪可装配战斗（开局 + 快照基线）。</summary>
        public bool MatchReady => _driver is { MatchStarted: true, HasSnapshot: true };

        /// <summary>状态/错误文本（错误以 "!" 前缀）。</summary>
        public event Action<string>? StatusChanged;

        /// <summary>房主：创建对局并监听；失败（如端口占用）→ 错误回调，Driver 保持 null。</summary>
        public void CreateAsHost()
        {
            if (_driver != null)
            {
                return;
            }

            try
            {
                INetMatchLink link = _hostFactoryOverride != null
                    ? _hostFactoryOverride()
                    : CreateHostLink();
                // 真实路径已在 CreateHostLink 内按实际监听端口写入；注入路径（测试）
                // 无 PeerHost，回落配置端口——大厅状态文本依赖该地址展示。
                ListenAddress ??= HostAddressPrefix + _listenPort.ToString(CultureInfo.InvariantCulture);
                _isHosting = true;
                _link = link;
                _driver = new NetMatchDriver(link, 0);
                _driver.Start(); // 本地 hello → 首次泵取座位 0 投影快照
                Raise("已创建对局，监听 " + ListenAddress + "。等待对手加入…");
            }
            catch (Exception e)
            {
                DisposePartial();
                Raise("!创建对局失败：" + e.Message);
            }
        }

        /// <summary>加入方：连接房主地址（"host:port"）；失败 → 错误回调，Driver 保持 null。</summary>
        public void ConnectAsJoiner(string address)
        {
            if (_driver != null)
            {
                return;
            }

            try
            {
                INetMatchLink link = _joinFactoryOverride != null
                    ? _joinFactoryOverride(address)
                    : CreateJoinLink(address);
                _isHosting = false;
                _link = link;
                _driver = new NetMatchDriver(link, 1);
                Raise("已连接房主，点击\"准备\"等待开局…");
            }
            catch (Exception e)
            {
                DisposePartial();
                Raise("!连接失败：" + e.Message);
            }
        }

        /// <summary>房主准备（流程引导；不门控开局，M12 语义）。仅房主角色有效。</summary>
        public void MarkHostReady()
        {
            if (!_isHosting)
            {
                return;
            }

            _link?.SendReady();
            Raise("已准备。等待对手加入后自动开局…");
        }

        /// <summary>加入方准备（SendReady；双方 Ready 后宿主自动开局）。仅加入方角色有效。</summary>
        public void SendJoinerReady()
        {
            if (_isHosting)
            {
                return;
            }

            _link?.SendReady();
            Raise("已准备。等待房主确认开局…");
        }

        /// <summary>大厅泵：驱动链路（Accept 对手 / 权威 tick / 回环与 TCP 下行）。</summary>
        public void Pump()
        {
            if (_driver == null)
            {
                return;
            }

            _driver.Pump();
            if (!_driver.LinkAlive && !_disconnectReported)
            {
                _disconnectReported = true;
                Raise("!连接已断开，对局无法继续。");
            }
        }

        public void Dispose()
        {
            _driver?.Dispose();
            _driver = null;
            _link = null;
        }

        // -----------------------------------------------------------------
        // 私有：真实链路创建
        // -----------------------------------------------------------------

        private INetMatchLink CreateHostLink()
        {
            MatchController controller = BattleComposition.StartMatch(
                _database, BattleComposition.BuildDeckKeys(_database));
            var matchHost = new MatchHost(controller);
            var peerHost = new PeerHost(matchHost, _clock, _listenPort);
            ListenAddress = HostAddressPrefix + peerHost.ListenPort.ToString(CultureInfo.InvariantCulture);
            return new HostNetLink(matchHost, peerHost);
        }

        private INetMatchLink CreateJoinLink(string address)
        {
            (string host, int port) = ParseAddress(address);
            var client = new NetworkMatchClient(BclTcpTransport.Connect(host, port));
            client.SendHello("Joiner");
            return new JoinNetLink(client);
        }

        private static (string Host, int Port) ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("地址不能为空。");
            }

            int sep = address.LastIndexOf(':');
            if (sep <= 0 || sep == address.Length - 1
                || !int.TryParse(address.Substring(sep + 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int port)
                || port < 1 || port > 65535)
            {
                throw new ArgumentException("地址格式应为 host:port（如 127.0.0.1:7777）。");
            }

            return (address.Substring(0, sep), port);
        }

        private void DisposePartial()
        {
            _driver?.Dispose();
            _driver = null;
            _link = null;
            _isHosting = false;
        }

        private void Raise(string text)
        {
            StatusChanged?.Invoke(text);
        }
    }
}
