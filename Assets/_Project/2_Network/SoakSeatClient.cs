using Card.Application.Match;
using Card.Core;
using Card.Domain.Match;

namespace Card.Network
{
    /// <summary>
    /// Soak 座位客户端（M14-T7）：一个座位的上行命令 + 下行状态应用（ClientStateSync）。
    /// 座位 0 走 <see cref="PeerHost.LocalClientEndpoint"/>（LoopbackEndpoint，房主镜像语义）；
    /// 座位 1 走 <see cref="NetworkMatchClient"/>（真实 TCP + 帧化 + 事件分发）。
    /// 两侧均从应用后的只读状态决策——数据路径与真实客户端一致（任务卡 §3）。
    /// </summary>
    public abstract class SoakSeatClient
    {
        private int _nextSeq = 1;

        protected SoakSeatClient(int seat)
        {
            Seat = seat;
        }

        /// <summary>座位 Id（0 = 房主本地镜像，1 = 加入方）。</summary>
        public int Seat { get; }

        /// <summary>本座位的客户端状态同步器（快照基线 + 增量应用）。</summary>
        public ClientStateSync Sync { get; } = new ClientStateSync();

        /// <summary>累计上行命令数。</summary>
        public int Submitted { get; private set; }

        /// <summary>累计被权威拒绝的命令数（含原因的拒绝步）。</summary>
        public int Rejected { get; private set; }

        /// <summary>累计断链事件数（座位 1 经事件感知；座位 0 无会话恒 0）。</summary>
        public int DisconnectedCount { get; private set; }

        /// <summary>最近一次断链原因（座位 1 经事件捕获；未断链为 null）。</summary>
        public string? LastDisconnectReason { get; protected set; }

        /// <summary>最近一条回执的命令序号（被拒命令也回执，M13-T3 口径）。</summary>
        public int LastReceiptSeq { get; private set; }

        /// <summary>首个拒绝回执的可读原因（诊断用；未被拒为 null）。</summary>
        public string? FirstRejectReason { get; private set; }

        /// <summary>座位 0：房主本地镜像端点（无帧化、无心跳会话）。</summary>
        public static SoakSeatClient CreateLocal(LoopbackEndpoint endpoint)
        {
            return new LocalSeatClient(endpoint);
        }

        /// <summary>座位 1：加入方客户端（真实 TCP，事件分发）。</summary>
        public static SoakSeatClient CreateRemote(NetworkMatchClient client)
        {
            return new RemoteSeatClient(client);
        }

        /// <summary>
        /// 上行一条命令（seq 信封；序号每座位独立从 1 递增）。返回本次序号供回执匹配。
        /// </summary>
        public int Submit(IGameCommand command)
        {
            int seq = _nextSeq++;
            Submitted++;
            SendRaw(BuildEnvelope(command, seq));
            return seq;
        }

        /// <summary>发送一帧原始 JSON 文本（信封或握手帧）。</summary>
        public abstract void SendRaw(string json);

        /// <summary>排空本座位的下行帧并应用到 Sync。</summary>
        public abstract void Pump();

        /// <summary>构造 { kind:"command", payload, seq } 信封（M13-T3 格式）。</summary>
        private static string BuildEnvelope(IGameCommand command, int seq)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(CommandSerializer.Serialize(command)))
                .Add("seq", JsonValue.From(seq))
                .ToJson();
        }

        /// <summary>应用一帧下行 JSON：snapshot 载入基线；step 统计裁决与回执序号。</summary>
        protected void Handle(JsonValue frame)
        {
            if (!frame.TryGetMember("kind", out JsonValue kind) || kind.Kind != JsonKind.String)
            {
                return;
            }

            if (kind.StringValue == "snapshot")
            {
                Sync.ApplySnapshot(frame);
                return;
            }

            if (kind.StringValue != "step")
            {
                return;
            }

            StepOutcome outcome = Sync.ApplyStep(frame);
            if (outcome.Status == StepApplyStatus.Rejected)
            {
                Rejected++;
                FirstRejectReason ??= "seq=" + outcome.Seq + " error=" + outcome.Error + " " + outcome.Detail;
            }

            if (outcome.Seq is int seq && seq > LastReceiptSeq)
            {
                LastReceiptSeq = seq;
            }
        }

        /// <summary>座位 0 实现：LoopbackEndpoint 直收直发（JSON 文本队列）。</summary>
        private sealed class LocalSeatClient : SoakSeatClient
        {
            private readonly LoopbackEndpoint _endpoint;

            public LocalSeatClient(LoopbackEndpoint endpoint)
                : base(seat: 0)
            {
                _endpoint = endpoint;
            }

            public override void SendRaw(string json)
            {
                _endpoint.Send(json);
            }

            public override void Pump()
            {
                while (_endpoint.TryReceive(out string raw))
                {
                    Handle(JsonValue.Parse(raw));
                }
            }
        }

        /// <summary>座位 1 实现：NetworkMatchClient 事件分发 + Pump 排空（自动回 Pong）。</summary>
        private sealed class RemoteSeatClient : SoakSeatClient
        {
            private readonly NetworkMatchClient _client;

            public RemoteSeatClient(NetworkMatchClient client)
                : base(seat: 1)
            {
                _client = client;
                client.SnapshotReceived += Handle;
                client.StepReceived += Handle;
                client.Disconnected += reason =>
                {
                    DisconnectedCount++;
                    LastDisconnectReason = reason;
                };
            }

            public override void SendRaw(string json)
            {
                _client.SendCommand(json);
            }

            public override void Pump()
            {
                _client.Pump();
            }
        }
    }
}
