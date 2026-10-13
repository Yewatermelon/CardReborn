using System;
using System.Collections.Generic;
using System.Threading;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Network
{
    /// <summary>
    /// Soak 回归驱动（M14-T7）：真实 TCP 回环连续跑 N 局双座位策略对打。
    /// 每局全链路：MatchFactory 建局 → MatchController → MatchHost → PeerHost →
    /// 客户端 Connect/握手 → Ready/快照 → 策略驱动至终局 → 收裁决 → 全资源 Dispose →
    /// 下一局重建（30 次建/拆监听，专抓 socket/句柄泄漏）。
    /// 单局失败（异常/等待超时/步数超限）记录 Failure 后继续，不中断批量。
    /// </summary>
    public static class NetworkSoakRunner
    {
        /// <summary>诊断环容量（覆盖心跳噪声，保证停摆前的关键事件不被挤出尾部）。</summary>
        private const int DiagCapacity = 1024;

        /// <summary>连续跑 MatchCount 局（每局 seed = BaseSeed + 局序）。</summary>
        public static NetworkSoakReport Run(CardDatabase database, NetworkSoakOptions options)
        {
            List<NetworkSoakMatchRecord> records = new List<NetworkSoakMatchRecord>();
            for (int i = 0; i < options.MatchCount; i++)
            {
                records.Add(RunOne(database, options, options.BaseSeed + i));
            }

            return new NetworkSoakReport(records);
        }

        /// <summary>跑一局并全量释放资源；任何异常转为 Failure 记录，不上抛。</summary>
        private static NetworkSoakMatchRecord RunOne(CardDatabase database, NetworkSoakOptions options, int seed)
        {
            NetworkSoakMatchRecord record = new NetworkSoakMatchRecord { Seed = seed };
            MatchContext? context = null;
            try
            {
                context = Connect(database, options, seed, record);
                DriveMatch(database, context, options, record);
                CollectOutcome(context, record);
            }
            catch (Exception exception)
            {
                record.Failure = exception.GetType().Name + ": " + exception.Message + DescribeLive(context);
            }
            finally
            {
                context?.Dispose();
            }

            return record;
        }

        /// <summary>异常现场诊断（断链定位用，M14-T7）：双端连接态、断链原因、会话超时态、接收线程健康。</summary>
        private static string DescribeLive(MatchContext? context)
        {
            if (context is null)
            {
                return string.Empty;
            }

            PeerSession? session = context.PeerHost.ActiveSession;
            return " [clientConnected=" + context.Client.Connected
                + ", disconnectReason=" + (context.Seat1.LastDisconnectReason ?? "-")
                + ", sessionTimedOut=" + (session?.TimedOut ?? false)
                + ", sessionConnected=" + (session?.IsConnected ?? false)
                + ", tick=" + context.Clock.CurrentTick
                + ", sessionIdle=" + (session is null ? "-" : (context.Clock.CurrentTick - session.LastActivityTick).ToString())
                + ", authVer=" + context.MatchHost.StateVersion
                + ", seat0Ver=" + VerOf(context.Seat0)
                + ", seat1Ver=" + VerOf(context.Seat1)
                + ", seat0Seq=" + context.Seat0.LastReceiptSeq
                + ", seat1Seq=" + context.Seat1.LastReceiptSeq
                + SoakDiagnostics.Describe(
                    context.Client, context.PeerHost, context.ClientDiag, context.HostDiag)
                + "]";
        }

        /// <summary>座位同步状态版本（未收到快照时为 -1）。</summary>
        private static string VerOf(SoakSeatClient seat)
        {
            return seat.Sync.HasSnapshot ? seat.Sync.LocalVersion.ToString() : "-1";
        }

        /// <summary>建局、起监听、握手、开局并等待双座位快照基线。</summary>
        private static MatchContext Connect(
            CardDatabase database, NetworkSoakOptions options, int seed, NetworkSoakMatchRecord record)
        {
            ManualClock clock = new ManualClock();
            MatchState state = MatchFactory.Create(
                database,
                new MatchSetupRequest(options.HostHeroKey, BuildDeck(database)),
                new MatchSetupRequest(options.PeerHeroKey, BuildDeck(database)),
                new SeededRandomProvider(seed));
            MatchContext context = BuildStack(clock, state, database);

            if (context.PeerHost.AcceptPeer() is null)
            {
                throw new InvalidOperationException("PeerHost.AcceptPeer 未接受连接。");
            }

            context.Client.SendHello("soak-peer");
            WaitOrThrow(context, () => context.Client.Handshook, options, record, "握手超时。");
            context.Client.SendReady();
            context.Seat0.SendRaw("{\"kind\":\"hello\"}");
            WaitOrThrow(
                context,
                () => context.Seat0.Sync.HasSnapshot && context.Seat1.Sync.HasSnapshot,
                options, record, "等待双座快照超时。");
            return context;
        }

        /// <summary>装配单局网络栈：权威三方（Clock/Controller/MatchHost）+ PeerHost + 双座位。</summary>
        private static MatchContext BuildStack(ManualClock clock, MatchState state, CardDatabase database)
        {
            MatchContext context = new MatchContext
            {
                Clock = clock,
                Controller = new MatchController(state, database, clock),
                ClientDiag = new NetworkDiagnostics(clock, DiagCapacity),
                HostDiag = new NetworkDiagnostics(clock, DiagCapacity),
            };
            context.MatchHost = new MatchHost(context.Controller);
            context.PeerHost = new PeerHost(context.MatchHost, clock, diag: context.HostDiag);
            context.Client = new NetworkMatchClient(
                BclTcpTransport.Connect("127.0.0.1", context.PeerHost.ListenPort), context.ClientDiag);
            context.Seat0 = SoakSeatClient.CreateLocal(context.PeerHost.LocalClientEndpoint);
            context.Seat1 = SoakSeatClient.CreateRemote(context.Client);
            return context;
        }

        /// <summary>策略驱动循环：行动方决策 → 上行 → 等回执，直至终局或步数超限。</summary>
        private static void DriveMatch(
            CardDatabase database, MatchContext context, NetworkSoakOptions options, NetworkSoakMatchRecord record)
        {
            int steps = 0;
            while (!context.MatchHost.IsFinished && steps < options.MaxStepsPerMatch)
            {
                int seat = context.MatchHost.ActiveSeat;
                SoakSeatClient actor = seat == context.PeerHost.HostSeat ? context.Seat0 : context.Seat1;
                IGameCommand command = SoakPolicy.NextCommand(ViewOf(actor, context), database, seat);
                int seq = actor.Submit(command);
                context.ClientDiag.Record(NetworkEventType.SendFrame, $"step={steps} seq={seq} actor=seat{seat}");
                if (!PumpUntil(context, () => actor.LastReceiptSeq >= seq, options.MaxPumpsPerWait, record))
                {
                    record.Failure = "等待命令回执超时（seq=" + seq + "，actor=seat" + seat + "）。" + DescribeLive(context);
                    return;
                }

                steps++;
            }

            if (!context.MatchHost.IsFinished)
            {
                record.Failure = "步数上限 " + options.MaxStepsPerMatch + " 内未终局。";
            }
        }

        /// <summary>行动视图：座位自己的同步状态（与真实客户端同口径；未就绪退回权威状态）。</summary>
        private static MatchState ViewOf(SoakSeatClient actor, MatchContext context)
        {
            return actor.Sync.HasSnapshot ? actor.Sync.State : context.Controller.State;
        }

        /// <summary>收终局裁决与统计快照。</summary>
        private static void CollectOutcome(MatchContext context, NetworkSoakMatchRecord record)
        {
            MatchOutcome? outcome = context.MatchHost.Outcome;
            record.Finished = context.MatchHost.IsFinished
                && outcome is not null
                && outcome.Result != MatchResult.Ongoing;
            record.WinnerId = outcome?.WinnerId;
            record.TurnNumber = context.Controller.State.TurnNumber;
            record.DisconnectCount = context.Seat1.DisconnectedCount;
            record.SubmissionCount = context.Seat0.Submitted + context.Seat1.Submitted;
            record.RejectedCount = context.Seat0.Rejected + context.Seat1.Rejected;
            record.FirstReject = context.Seat0.FirstRejectReason ?? context.Seat1.FirstRejectReason;
        }

        /// <summary>按规则配置的牌组张数，从启用卡池循环取键补足（卡组允许重复）。</summary>
        private static IReadOnlyList<string> BuildDeck(CardDatabase database)
        {
            IReadOnlyList<CardDefinition> pool = database.FilterCards();
            if (pool.Count == 0)
            {
                throw new InvalidOperationException("启用卡池为空，无法组建 Soak 牌组。");
            }

            int deckSize = database.Rules.DeckSize;
            List<string> deck = new List<string>(deckSize);
            while (deck.Count < deckSize)
            {
                for (int i = 0; i < pool.Count && deck.Count < deckSize; i++)
                {
                    deck.Add(pool[i].Key);
                }
            }

            return deck;
        }

        /// <summary>泵等待条件成立；超限抛 InvalidOperationException（由 RunOne 转 Failure）。</summary>
        private static void WaitOrThrow(
            MatchContext context, Func<bool> condition, NetworkSoakOptions options,
            NetworkSoakMatchRecord record, string message)
        {
            if (!PumpUntil(context, condition, options.MaxPumpsPerWait, record))
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>逐 tick 推进网络直至条件成立；每次推进计入 HeartbeatLoops。</summary>
        private static bool PumpUntil(
            MatchContext context, Func<bool> condition, int maxPumps, NetworkSoakMatchRecord record)
        {
            for (int i = 0; i < maxPumps; i++)
            {
                context.Clock.Advance(1);
                context.PeerHost.RunNetworkTick();
                context.Seat0.Pump();
                context.Client.Pump();
                record.HeartbeatLoops++;
                if (condition())
                {
                    return true;
                }

                RescueHeartbeat(context);
                Thread.Sleep(1);
            }

            return false;
        }

        /// <summary>
        /// 心跳追投递（M14-T7）：等待期间宿主活动计时逼近超时（回环异步投递在调度抖动下
        /// 可迟到整块，见 SoakTestHarness.RescueIdle 注释）时，冻结时钟补泵直至 Pong/命令
        /// 落袋刷新活动计时；连接真死时轮次耗尽，心跳超时照常发生。
        /// </summary>
        private static void RescueHeartbeat(MatchContext context)
        {
            PeerSession? session = context.PeerHost.ActiveSession;
            if (session is null || session.TimedOut || !session.IsConnected)
            {
                return;
            }

            int limit = PeerHost.TimeoutTicks - 15;
            for (int round = 0; round < 60 && context.Clock.CurrentTick - session.LastActivityTick >= limit; round++)
            {
                Thread.Sleep(1);
                context.PeerHost.RunNetworkTick();
                context.Seat0.Pump();
                context.Client.Pump();
            }
        }

        /// <summary>单局资源束：Clock/Controller/MatchHost/PeerHost/Client + 双座位客户端。</summary>
        private sealed class MatchContext : IDisposable
        {
            public ManualClock Clock = null!;
            public MatchController Controller = null!;
            public MatchHost MatchHost = null!;
            public PeerHost PeerHost = null!;
            public NetworkMatchClient Client = null!;
            public SoakSeatClient Seat0 = null!;
            public SoakSeatClient Seat1 = null!;
            public NetworkDiagnostics ClientDiag = null!;
            public NetworkDiagnostics HostDiag = null!;
            private bool _disposed;

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
}
