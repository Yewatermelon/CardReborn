using System;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Bootstrap.Battle.Net;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// 联机大厅状态机测试（M14-T3 AC-10/11）：fake 链路工厂注入——
    /// 创建失败 / 地址非法 / 成功交付驱动 / 准备引导 / MatchReady 判定 / 断链报告。
    /// </summary>
    [TestFixture]
    public class PvpLobbyFlowTests
    {
        private CardDatabase _database = null!;
        private MatchState _state = null!;
        private FakeNetLink _hostLink = null!;
        private FakeNetLink _joinLink = null!;
        private PvpLobbyFlow _flow = null!;
        private string? _lastStatus;

        [SetUp]
        public void SetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _state = MatchControllerFixtures.NewMatch(
                _database, MatchControllerFixtures.LoadEnabledDeckKeys(), seed: 42);
            _hostLink = new FakeNetLink();
            _joinLink = new FakeNetLink();
            _flow = new PvpLobbyFlow(_database, PvpLobbyFlow.DefaultListenPort, new ManualClock(),
                () => _hostLink, _ => _joinLink);
            _flow.StatusChanged += text => _lastStatus = text;
        }

        private JsonValue SnapshotFrame(int version)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("snapshot"))
                .Add("version", JsonValue.From(version))
                .Add("state", MatchStateSerializer.WriteStateTree(_state));
        }

        [Test]
        public void CreateAsHost_Success_DeliversDriverWithListenAddress()
        {
            _flow.CreateAsHost();

            Assert.That(_flow.Driver, Is.Not.Null);
            Assert.That(_flow.IsHosting, Is.True);
            Assert.That(_flow.ListenAddress, Does.StartWith("127.0.0.1:"));
            Assert.That(_lastStatus, Does.Contain("等待对手"));
        }

        [Test]
        public void CreateAsHost_FactoryThrows_ReportsErrorAndNoDriver()
        {
            var flow = new PvpLobbyFlow(_database, PvpLobbyFlow.DefaultListenPort, new ManualClock(),
                () => throw new InvalidOperationException("端口被占用"), null);
            string? status = null;
            flow.StatusChanged += text => status = text;

            flow.CreateAsHost();

            Assert.That(flow.Driver, Is.Null);
            Assert.That(status, Does.StartWith("!"));
            Assert.That(status, Does.Contain("端口被占用"));
        }

        [Test]
        public void ConnectAsJoiner_IllegalAddress_ReportsErrorAndNoDriver()
        {
            // 无 override 注入 → 走真实路径的地址解析（ParseAddress 先抛，不会真连 TCP）
            var flow = new PvpLobbyFlow(_database, PvpLobbyFlow.DefaultListenPort, new ManualClock());
            string? status = null;
            flow.StatusChanged += text => status = text;

            flow.ConnectAsJoiner("没有地址");

            Assert.That(flow.Driver, Is.Null);
            Assert.That(status, Does.StartWith("!"));
            Assert.That(status, Does.Contain("地址格式"));
        }

        [Test]
        public void JoinerFlow_StartedWithSnapshot_BecomesMatchReady()
        {
            _flow.ConnectAsJoiner("127.0.0.1:7777");
            Assert.That(_flow.MatchReady, Is.False);
            Assert.That(_flow.IsHosting, Is.False);

            _joinLink.MatchStarted = true;
            _joinLink.Downlink.Enqueue(SnapshotFrame(0));
            _flow.Pump();

            Assert.That(_flow.MatchReady, Is.True);
            Assert.That(_flow.Driver!.HasSnapshot, Is.True);
        }

        [Test]
        public void HostFlow_SnapshotOnly_IsNotReadyUntilStarted()
        {
            _flow.CreateAsHost();
            _hostLink.Downlink.Enqueue(SnapshotFrame(0));
            _flow.Pump();

            Assert.That(_flow.Driver!.HasSnapshot, Is.True);
            Assert.That(_flow.MatchReady, Is.False, "对手未 Ready 不应视为开局");

            _hostLink.MatchStarted = true;
            Assert.That(_flow.MatchReady, Is.True);
        }

        [Test]
        public void ReadyActions_ForwardToLink()
        {
            // 一个 flow 实例只扮演一个角色：房主准备转发房主链路，
            // 加入方准备转发加入链路，互不串扰（角色 guard）。
            _flow.CreateAsHost();
            _flow.MarkHostReady();
            Assert.That(_hostLink.ReadyCount, Is.EqualTo(1));

            var joinFlow = new PvpLobbyFlow(_database, PvpLobbyFlow.DefaultListenPort, new ManualClock(),
                () => _hostLink, _ => _joinLink);
            joinFlow.ConnectAsJoiner("127.0.0.1:7777");
            joinFlow.SendJoinerReady();
            Assert.That(_joinLink.ReadyCount, Is.EqualTo(1));
            Assert.That(_hostLink.ReadyCount, Is.EqualTo(1), "加入方准备不得误发房主链路");

            joinFlow.MarkHostReady();
            Assert.That(_joinLink.ReadyCount, Is.EqualTo(1), "房主准备动作在加入方角色下不应生效");
        }

        [Test]
        public void LobbyPump_Disconnect_ReportsErrorOnce()
        {
            _flow.CreateAsHost();
            _hostLink.Alive = false;

            _flow.Pump();
            _flow.Pump();

            Assert.That(_lastStatus, Does.StartWith("!"));
            Assert.That(_lastStatus, Does.Contain("断开"));
        }

        [Test]
        public void CreateAsHost_Twice_SecondCallIgnored()
        {
            _flow.CreateAsHost();
            FakeNetLink before = _hostLink;
            _flow.CreateAsHost();

            Assert.That(_flow.Driver, Is.Not.Null);
            Assert.That(before, Is.SameAs(_hostLink));
        }
    }
}
