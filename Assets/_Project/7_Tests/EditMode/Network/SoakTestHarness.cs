using System;
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
    /// M14-T7 共享夹具：真实 TCP 回环网络栈封装（握手 / 开局 / 空转泵）。
    /// 与 PeerHostTickTests 同模式（接收线程异步入队，自旋等待 + 让路）。
    /// </summary>
    internal sealed class SoakStack : IDisposable
    {
        private bool _disposed;

        public ManualClock Clock { get; private set; } = null!;
        public MatchController Controller { get; private set; } = null!;
        public MatchHost MatchHost { get; private set; } = null!;
        public PeerHost PeerHost { get; private set; } = null!;
        public PeerSession Session { get; private set; } = null!;
        public NetworkDiagnostics Diag { get; private set; } = null!;
        public NetworkDiagnostics HostDiag { get; private set; } = null!;
        public NetworkMatchClient Client { get; private set; } = null!;
        public SoakSeatClient Seat0 { get; private set; } = null!;
        public SoakSeatClient Seat1 { get; private set; } = null!;

        /// <summary>建栈 + 真实 TCP 握手（不 Ready、不开局）。</summary>
        public static SoakStack Connect(CardDatabase database, int seed)
        {
            ManualClock clock = new ManualClock();
            MatchState state = MatchControllerFixtures.NewMatch(
                database, MatchControllerFixtures.LoadEnabledDeckKeys(), seed);
            MatchController controller = new MatchController(state, database, clock);
            MatchHost matchHost = new MatchHost(controller);
            NetworkDiagnostics diag = new NetworkDiagnostics(clock);
            NetworkDiagnostics hostDiag = new NetworkDiagnostics(clock);
            PeerHost peerHost = new PeerHost(matchHost, clock, diag: hostDiag);
            NetworkMatchClient client = new NetworkMatchClient(
                BclTcpTransport.Connect("127.0.0.1", peerHost.ListenPort), diag);
            SoakStack stack = new SoakStack
            {
                Clock = clock,
                Controller = controller,
                MatchHost = matchHost,
                PeerHost = peerHost,
                Diag = diag,
                HostDiag = hostDiag,
                Client = client,
                Seat0 = SoakSeatClient.CreateLocal(peerHost.LocalClientEndpoint),
                Seat1 = SoakSeatClient.CreateRemote(client),
            };
            stack.Session = peerHost.AcceptPeer()!;
            client.SendHello("soak-peer");
            Assert.That(stack.PumpUntil(() => client.Handshook, 200), Is.True, "握手超时。");
            return stack;
        }

        /// <summary>Ready 开局并等待两个座位的快照基线就绪。</summary>
        public void StartMatch()
        {
            Client.SendReady();
            Seat0.SendRaw("{\"kind\":\"hello\"}");
            Assert.That(PumpUntil(() => Seat0.Sync.HasSnapshot && Seat1.Sync.HasSnapshot, 400),
                Is.True, "等待双座快照超时。");
        }

        /// <summary>
        /// 分块空转（块长必须小于心跳超时 60 ticks，逐块回 Pong）。
        /// 每块后在同一模拟 tick 内重试泵数次：PeerSession 只有收到的帧才重置活动计时，
        /// Pong 经异步接收线程落袋需要真实时间，不推进时钟的重试给它落袋窗口。
        /// 重试次数取 8：机器高负载（如并行构建）下单次重试可能全部错过，8 次仍全错过
        /// 会使宿主 idle 连跨两块（≥60）误判超时——实测 4 次在构建争用下出现过一次。
        /// </summary>
        public void PumpIdle(int totalTicks, int chunkTicks = 30)
        {
            for (int done = 0; done < totalTicks; done += chunkTicks)
            {
                Clock.Advance(Math.Min(chunkTicks, totalTicks - done));
                PumpRound();
                for (int retry = 0; retry < 8; retry++)
                {
                    Thread.Sleep(1);
                    PumpRound();
                }

                RescueIdle();
            }
        }

        /// <summary>
        /// 自救泵（M14-T7）：空闲块收尾时若宿主活动计时逼近心跳窗口（Pong 尚未落袋），
        /// 在冻结时钟下继续泵直至活动计时刷新——只在连接真正存活时有效；
        /// 连接真死时自救轮次耗尽，心跳超时照常发生（测试如实失败）。
        /// 触发阈值取 25（< 心跳间隔 30）：实测回环异步投递在调度抖动下可迟到整块
        /// （Ping 发出后客户端下一块才收到），60-tick 窗口只剩 2 块余量，必须每块追平。
        /// </summary>
        private void RescueIdle()
        {
            PeerSession? session = PeerHost.ActiveSession;
            if (session is null || session.TimedOut)
            {
                return;
            }

            int limit = PeerHost.HeartbeatIntervalTicks - 5;
            for (int round = 0; round < 60 && Clock.CurrentTick - session.LastActivityTick >= limit; round++)
            {
                Thread.Sleep(1);
                PumpRound();
            }
        }

        /// <summary>单次网络泵：宿主 tick + 双座位排空。</summary>
        private void PumpRound()
        {
            PeerHost.RunNetworkTick();
            Seat0.Pump();
            Client.Pump();
        }

        /// <summary>逐 tick 推进直至条件成立；返回 false 表示泵上限内未达成。</summary>
        public bool PumpUntil(Func<bool> condition, int maxPumps)
        {
            for (int i = 0; i < maxPumps; i++)
            {
                Clock.Advance(1);
                PeerHost.RunNetworkTick();
                Seat0.Pump();
                Client.Pump();
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(1);
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Client.Dispose();
            PeerHost.Dispose();
        }
    }
}
