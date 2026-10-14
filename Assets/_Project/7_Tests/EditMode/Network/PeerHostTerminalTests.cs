using System;
using System.Collections.Generic;
using System.Threading;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Network;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// 异常与终态测试（M13-T8 / AC-3…AC-6）：回合操作超时代发结束回合、掉线判负
    /// （重连取消计时、窗口超时判负）、权威代发路径与 MatchEnd 帧化、房主退出终态。
    /// 真实 TCP 回环 + ManualClock 快进 + 自旋等待（与 PeerHostReconnectTests 同模式）。
    /// </summary>
    [TestFixture]
    public class PeerHostTerminalTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;
        private MatchController _controller = null!;
        private MatchHost _matchHost = null!;
        private PeerHost _peerHost = null!;
        private ManualClock _clock = null!;
        private ITransport _client = null!;
        private PeerSession? _session;
        private Card.Tests.EditMode.Core.RecordingLogSink _logSink = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _logSink = new Card.Tests.EditMode.Core.RecordingLogSink();
            GameLog.Configure(_logSink, LogLevel.Info);
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        [TearDown]
        public void TearDown()
        {
            if (TestContext.CurrentContext.Result.FailCount > 0)
            {
                TestContext.Out.WriteLine("---- Network 日志（诊断）----");
                foreach (var entry in _logSink.Entries)
                {
                    TestContext.Out.WriteLine($"[{entry.Level}] {entry.Message}");
                }
            }

            _client?.Dispose();
            _peerHost?.Dispose();
        }

        /// <summary>反复驱动网络 tick 直到指定端收到一帧（上限 2 秒）。</summary>
        private RawMessage ReceiveFrameAfterTick(ITransport clientSide)
        {
            for (int i = 0; i < 200; i++)
            {
                _peerHost.RunNetworkTick();
                if (clientSide.TryReceive(out byte[]? frame))
                {
                    return MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
                }

                Thread.Sleep(10);
            }

            Assert.Fail("等待网络帧超时（2 秒）。");
            throw new InvalidOperationException("unreachable");
        }

        /// <summary>标准开局：握手 → Ready → MatchStart + 初始快照（version 0）。</summary>
        private void StartMatch()
        {
            _controller = new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
            _matchHost = new MatchHost(_controller);
            _clock = new ManualClock();
            _peerHost = new PeerHost(_matchHost, _clock);
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            _session = _peerHost.AcceptPeer();
            Assert.That(_session, Is.Not.Null);

            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2"));
            _client.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.HelloAck));
            _client.Send(MessageCodec.Frame(NetworkMessageType.Ready, MessageCodec.CurrentProtocolVersion, "{}"));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.MatchStart));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.MatchSnapshot));
            Assert.That(_peerHost.MatchLive, Is.True, "开局后对局应已激活");
        }

        /// <summary>快进指定 tick 并驱动一次网络 tick。</summary>
        private void AdvanceAndTick(int ticks)
        {
            _clock.Advance(ticks);
            _peerHost.RunNetworkTick();
        }

        /// <summary>
        /// 分批快进（每批 ≤50 tick，批间整批上行 Pong 保持会话活跃）——模拟"在线但不出牌"，
        /// 避免长快进触发 PeerSession 心跳超时（60 tick）与掉线判负路径（回合超时用例专用）。
        /// 每批发整批帧而非单帧：编辑器高负载下 IO 接收线程调度延迟可超任何固定 Sleep，
        /// 单帧迟到即触发心跳超时误判负（M14-T3 复跑暴露），整批缓存后接收线程醒一次即可清零 idle。
        /// M15-B2：批末用 <see cref="PeerSession.LastActivityTick"/> 自旋确认宿主已消费本批，
        /// 再推下一批时钟——否则连续两拍接收延迟即 idle≥60 心跳超时断链（宿主 Dispose 传输，
        /// 客户端后续 Send 抛"TCP 传输已断开"，M15-T1 复跑暴露）。确认后 idle 最多累积 50。
        /// </summary>
        private void AdvanceIdle(int totalTicks)
        {
            int remaining = totalTicks;
            while (remaining > 0)
            {
                int step = Math.Min(remaining, 50);
                _clock.Advance(step);
                remaining -= step;
                for (int i = 0; i < step; i++)
                {
                    JsonValue pong = JsonValue.Object().Add("timestamp", JsonValue.From(_clock.CurrentTick));
                    _client.Send(MessageCodec.Frame(NetworkMessageType.Pong, MessageCodec.CurrentProtocolVersion, pong.ToJson()));
                }

                WaitForBatchConsumed();
            }
        }

        /// <summary>自旋驱动网络 tick 直到宿主会话消费到本批 Pong（activity 追平时钟），上限 2 秒。</summary>
        private void WaitForBatchConsumed()
        {
            for (int i = 0; i < 200; i++)
            {
                _peerHost.RunNetworkTick();
                if (_session != null && _session.LastActivityTick >= _clock.CurrentTick)
                {
                    return;
                }

                Thread.Sleep(10);
            }

            Assert.Fail("等待宿主消费本批 Pong 超时（2 秒）——会话可能已心跳超时断链。");
        }

        /// <summary>自旋等待宿主侧会话感知掉线（上限 2 秒）。</summary>
        private void WaitForDisconnect()
        {
            for (int i = 0; i < 200; i++)
            {
                _peerHost.RunNetworkTick();
                if (_session != null && !_session.IsConnected)
                {
                    return;
                }

                Thread.Sleep(10);
            }

            Assert.Fail("等待掉线感知超时（2 秒）。");
        }

        // -----------------------------------------------------------------
        // AC-3: 回合操作超时——75 s 无权威状态变更自动结束回合，计时随 version 推进重置
        // -----------------------------------------------------------------

        [Test]
        public void TurnTimeout_AutoEndsTurn_AfterIdleTicks()
        {
            StartMatch();
            int firstSeat = _matchHost.ActiveSeat;

            AdvanceIdle(PeerHost.TurnTimeoutTicks - 1);
            Assert.That(_matchHost.StateVersion, Is.EqualTo(0UL), "不足超时阈值不应代发");

            AdvanceAndTick(1);
            Assert.That(_matchHost.StateVersion, Is.EqualTo(1UL), "75s 无动作应自动结束当前回合");
            Assert.That(_matchHost.ActiveSeat, Is.EqualTo(1 - firstSeat), "自动结束回合后应切换行动方");

            // 计时随代发重置：不会立即连续代发
            AdvanceIdle(PeerHost.TurnTimeoutTicks - 1);
            Assert.That(_matchHost.StateVersion, Is.EqualTo(1UL), "version 推进后计时应重置");
            AdvanceAndTick(1);
            Assert.That(_matchHost.StateVersion, Is.EqualTo(2UL));
            Assert.That(_matchHost.ActiveSeat, Is.EqualTo(firstSeat));
        }

        // -----------------------------------------------------------------
        // AC-4: 掉线判负——窗口内重连取消计时；窗口超时判负（对手胜）
        // -----------------------------------------------------------------

        [Test]
        public void Disconnect_ReconnectCancelsTimer_WindowExpiresForfeits()
        {
            StartMatch();

            // 断开对手 TCP → 宿主侧感知掉线 → 窗口内重连取消判负计时
            _client.Dispose();
            WaitForDisconnect();
            AdvanceAndTick(PeerHost.ReconnectWindowTicks / 2);
            _client = BclTcpTransport.Connect("127.0.0.1", _peerHost.ListenPort);
            PeerSession? reconnected = _peerHost.AcceptPeer();
            Assert.That(reconnected, Is.Not.Null, "窗口内重连应被接受");
            _session = reconnected;

            // 重连握手（真实客户端行为：重连后立即 Hello，保持新会话活跃）
            JsonValue hello = JsonValue.Object().Add("playerName", JsonValue.From("p2-reconnect"));
            _client.Send(MessageCodec.Frame(NetworkMessageType.Hello, MessageCodec.CurrentProtocolVersion, hello.ToJson()));
            Assert.That(ReceiveFrameAfterTick(_client).Type, Is.EqualTo(NetworkMessageType.HelloAck));

            AdvanceIdle(PeerHost.ReconnectWindowTicks + 100);
            Assert.That(_matchHost.IsFinished, Is.False, "重连应取消判负计时");

            // 再次断开 → 窗口超时判负：座位 1 判负、对手（座位 0）获胜
            _client.Dispose();
            WaitForDisconnect();
            AdvanceAndTick(PeerHost.ReconnectWindowTicks - 1);
            Assert.That(_matchHost.IsFinished, Is.False, "窗口内不应判负");
            AdvanceAndTick(1);
            Assert.That(_matchHost.IsFinished, Is.True, "重连窗口超时应判负");
            Assert.That(_matchHost.Outcome, Is.Not.Null);
            Assert.That(_matchHost.Outcome!.WinnerId, Is.EqualTo(0));
            Assert.That(_peerHost.LastMatchEndReason, Is.EqualTo("Disconnect"));
        }

        // -----------------------------------------------------------------
        // AC-5: 权威代发路径（SubmitAuthority）→ 判负 step 下发 + MatchEnd 帧只发一次
        // -----------------------------------------------------------------

        [Test]
        public void AuthorityForfeit_BroadcastsDelta_AndMatchEndOnce()
        {
            StartMatch();

            CommandResult result = _matchHost.SubmitAuthority(new ForfeitCommand(1));
            Assert.That(result.IsValid, Is.True, "权威代发 Forfeit 应有效");
            Assert.That(_matchHost.IsFinished, Is.True);
            Assert.That(_matchHost.StateVersion, Is.EqualTo(1UL), "权威代发也应推进版本");

            RawMessage first = ReceiveFrameAfterTick(_client);
            Assert.That(first.Type, Is.EqualTo(NetworkMessageType.StateDelta), "判负结算应产生 step 下发");

            RawMessage second = ReceiveFrameAfterTick(_client);
            Assert.That(second.Type, Is.EqualTo(NetworkMessageType.MatchEnd));
            JsonValue end = JsonValue.Parse(second.PayloadJson);
            Assert.That(end.TryGetMember("winner", out JsonValue winner) && winner.IntValue == 0,
                Is.True, "MatchEnd winner 应为对手（座位 0）");
            Assert.That(end.TryGetMember("reason", out JsonValue reason) && reason.StringValue == "Normal",
                Is.True, "非掉线判负路径 reason 应为 Normal");

            _peerHost.RunNetworkTick();
            _peerHost.RunNetworkTick();
            Assert.That(_client.TryReceive(out _), Is.False, "MatchEnd 不应重复下发");
        }

        // -----------------------------------------------------------------
        // AC-6: 房主中途退出——尽力通知对手 HostQuit，Dispose 幂等
        // -----------------------------------------------------------------

        [Test]
        public void Dispose_DuringMatch_SendsHostQuitEnd()
        {
            StartMatch();

            _peerHost.Dispose();

            RawMessage? end = TryTakeFrame(_client);
            Assert.That(end, Is.Not.Null, "房主退出应尽力通知对手");
            Assert.That(end!.Value.Type, Is.EqualTo(NetworkMessageType.MatchEnd));
            JsonValue endJson = JsonValue.Parse(end!.Value.PayloadJson);
            Assert.That(endJson.TryGetMember("winner", out JsonValue winner) && winner.Kind == JsonKind.Null,
                Is.True, "作废对局 winner 应为 null");
            Assert.That(endJson.TryGetMember("reason", out JsonValue reason) && reason.StringValue == "HostQuit",
                Is.True);

            Assert.DoesNotThrow(() => _peerHost.Dispose(), "Dispose 应幂等");
        }

        /// <summary>自旋等待客户端侧收到一帧并返回（上限 2 秒；不吞帧——取到即返回）。</summary>
        private static RawMessage? TryTakeFrame(ITransport clientSide)
        {
            for (int i = 0; i < 200; i++)
            {
                if (clientSide.TryReceive(out byte[]? frame))
                {
                    return MessageCodec.TryUnframe(frame!, 0, frame!.Length, out _)!.Value;
                }

                Thread.Sleep(10);
            }

            return null;
        }
    }
}
