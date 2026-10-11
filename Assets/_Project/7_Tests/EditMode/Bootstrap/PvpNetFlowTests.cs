using System;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Bootstrap.Battle.Net;
using Card.Network;
using Card.Presentation.Battle.Feedback;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// 联机全链路端到端测试（M14-T3 AC-7/9，真实 TCP 回环）：
    /// PeerHost 房主 + JoinNetLink 加入方，双 <see cref="NetMatchDriver"/>——
    /// 连接 → 握手 → 准备 → 双方按座位快照 → 命令交替 → 版本一致推进 → 终局。
    /// </summary>
    [TestFixture]
    public class PvpNetFlowTests
    {
        private CardDatabase _database = null!;
        private MatchController _controller = null!;
        private MatchHost _matchHost = null!;
        private ManualClock _clock = null!;
        private PeerHost _peerHost = null!;
        private INetMatchLink _hostLink = null!;
        private INetMatchLink _joinLink = null!;
        private NetworkMatchClient _client = null!;
        private NetMatchDriver _hostDriver = null!;
        private NetMatchDriver _joinDriver = null!;

        [SetUp]
        public void SetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _controller = new MatchController(
                MatchControllerFixtures.NewMatch(_database, MatchControllerFixtures.LoadEnabledDeckKeys(), seed: 42),
                _database);
            _matchHost = new MatchHost(_controller);
            _clock = new ManualClock();
            _peerHost = new PeerHost(_matchHost, _clock, listenPort: 0);

            _hostLink = new HostNetLink(_matchHost, _peerHost);
            _hostDriver = new NetMatchDriver(_hostLink, 0);
            _hostDriver.Start(); // 房主本地 hello → 回环快照

            _client = new NetworkMatchClient(BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort));
            _client.SendHello("Joiner");
            _joinLink = new JoinNetLink(_client);
            _joinDriver = new NetMatchDriver(_joinLink, 1);
        }

        [TearDown]
        public void TearDown()
        {
            _hostDriver?.Dispose();
            _joinDriver?.Dispose();
        }

        private static string CommandEnvelope(IGameCommand command, int seq)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(command)))
                .Add("seq", JsonValue.From(seq))
                .ToJson();
        }

        private void PumpUntil(Func<bool> condition, int maxRounds = 500)
        {
            for (int i = 0; i < maxRounds && !condition(); i++)
            {
                _hostDriver.Pump();
                _joinDriver.Pump();
            }
        }

        /// <summary>握手 + 双方快照基线就绪 + 双方准备 → 双侧 MatchStarted。</summary>
        private void ReadyUp()
        {
            PumpUntil(() => _joinDriver.HasSnapshot && _hostDriver.HasSnapshot);
            _peerHost.SetHostReady();
            _joinLink.SendReady();
            PumpUntil(() => _joinDriver.MatchStarted && _hostDriver.MatchStarted);
        }

        [Test]
        public void EndToEnd_Handshake_BothDriversReceiveSeatSnapshots()
        {
            PumpUntil(() => _joinDriver.HasSnapshot && _hostDriver.HasSnapshot);

            Assert.That(_client.Handshook, Is.True);
            Assert.That(_hostDriver.HasSnapshot, Is.True);
            Assert.That(_joinDriver.HasSnapshot, Is.True);
            Assert.That(_hostDriver.Sync.LocalVersion, Is.EqualTo(_joinDriver.Sync.LocalVersion));
            Assert.That(_hostDriver.Sync.State.ActivePlayerId,
                Is.EqualTo(_joinDriver.Sync.State.ActivePlayerId));
        }

        [Test]
        public void EndToEnd_BothReady_MatchStartedOnBothSides()
        {
            PumpUntil(() => _joinDriver.HasSnapshot && _hostDriver.HasSnapshot);

            // M12 语义：房主 Ready 只是流程引导，不门控开局
            _peerHost.SetHostReady();
            PumpUntil(() => _hostDriver.HasSnapshot, 10);
            Assert.That(_peerHost.MatchLive, Is.False, "对手未 Ready 不应开局");

            _joinLink.SendReady();
            PumpUntil(() => _joinDriver.MatchStarted && _hostDriver.MatchStarted);

            Assert.That(_client.MatchStarted, Is.True);
            Assert.That(_peerHost.MatchLive, Is.True);
        }

        [Test]
        public void EndToEnd_EndTurn_BothSidesProgressInSync()
        {
            ReadyUp();

            int active = _hostDriver.Sync.State.ActivePlayerId;
            NetMatchDriver activeDriver = active == 0 ? _hostDriver : _joinDriver;
            ulong versionBefore = _hostDriver.Sync.LocalVersion;

            activeDriver.SendCommand(CommandEnvelope(new EndTurnCommand(active), 1));
            PumpUntil(() => _hostDriver.Sync.LocalVersion > versionBefore
                && _joinDriver.Sync.LocalVersion > versionBefore);

            Assert.That(_hostDriver.Sync.LocalVersion, Is.EqualTo(_joinDriver.Sync.LocalVersion));
            Assert.That(_hostDriver.Sync.State.ActivePlayerId, Is.Not.EqualTo(active));
            Assert.That(_joinDriver.Sync.State.ActivePlayerId,
                Is.EqualTo(_hostDriver.Sync.State.ActivePlayerId));
        }

        [Test]
        public void EndToEnd_RejectedCommand_NoVersionAdvance()
        {
            ReadyUp();

            int active = _hostDriver.Sync.State.ActivePlayerId;
            int idle = 1 - active;
            NetMatchDriver idleDriver = idle == 0 ? _hostDriver : _joinDriver;
            ulong versionBefore = _hostDriver.Sync.LocalVersion;
            StepOutcome? outcome = null;
            idleDriver.StepApplied += o => outcome ??= o;

            idleDriver.SendCommand(CommandEnvelope(new EndTurnCommand(idle), 1));
            PumpUntil(() => outcome != null);

            Assert.That(outcome!.Status, Is.EqualTo(StepApplyStatus.Rejected));
            Assert.That(_hostDriver.Sync.LocalVersion, Is.EqualTo(versionBefore));
            Assert.That(_joinDriver.Sync.LocalVersion, Is.EqualTo(versionBefore));
        }

        [Test]
        public void EndToEnd_Forfeit_MatchFinishesWithOpponentAsWinner()
        {
            ReadyUp();

            int forfeiter = _hostDriver.Sync.State.ActivePlayerId;
            NetMatchDriver forfeiterDriver = forfeiter == 0 ? _hostDriver : _joinDriver;

            forfeiterDriver.SendCommand(CommandEnvelope(new ForfeitCommand(forfeiter), 1));
            PumpUntil(() => _hostDriver.IsFinished && _joinDriver.IsFinished);

            Assert.That(_hostDriver.WinnerId, Is.EqualTo(1 - forfeiter));
            Assert.That(_joinDriver.WinnerId, Is.EqualTo(1 - forfeiter));
        }

        [Test]
        public void EndToEnd_CommandSink_PendingResolvedByAuthorityStep()
        {
            ReadyUp();

            var tracker = new PendingCommandTracker();
            var feedback = new PendingActionFeedback(tracker, null, null, null);
            int active = _hostDriver.Sync.State.ActivePlayerId;
            NetMatchDriver activeDriver = active == 0 ? _hostDriver : _joinDriver;
            var sink = new PvpCommandSink(activeDriver, feedback);

            CommandResult result = sink.Submit(new EndTurnCommand(active));
            Assert.That(result.IsValid, Is.True);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));

            PumpUntil(() => tracker.PendingCount == 0);
            Assert.That(tracker.PendingCount, Is.EqualTo(0), "权威回执应裁决在途登记");
        }
    }
}
