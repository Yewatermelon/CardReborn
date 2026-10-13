using System.Collections.Generic;
using System.Linq;

namespace Card.Network
{
    /// <summary>单局 Soak 记录（M14-T7）：一局的终局裁决、统计与失败原因。</summary>
    public sealed class NetworkSoakMatchRecord
    {
        /// <summary>本局随机种子（BaseSeed + 局序）。</summary>
        public int Seed { get; set; }

        /// <summary>是否正常终局（收到终局裁决且非 Ongoing）。</summary>
        public bool Finished { get; set; }

        /// <summary>胜者座位（null = 平局或未终局）。</summary>
        public int? WinnerId { get; set; }

        /// <summary>终局（或截止）时的回合数。</summary>
        public int TurnNumber { get; set; }

        /// <summary>累计上行命令数。</summary>
        public int SubmissionCount { get; set; }

        /// <summary>累计被权威拒绝的命令数。</summary>
        public int RejectedCount { get; set; }

        /// <summary>首个拒绝回执的可读原因（诊断定位用；未被拒为 null）。</summary>
        public string? FirstReject { get; set; }

        /// <summary>累计断链事件数。</summary>
        public int DisconnectCount { get; set; }

        /// <summary>累计驱动的网络 tick 数（RunNetworkTick 调用次数）。</summary>
        public int HeartbeatLoops { get; set; }

        /// <summary>失败原因（异常 / 等待超时 / 步数超限；正常终局为 null）。</summary>
        public string? Failure { get; set; }
    }

    /// <summary>Soak 汇总报告（M14-T7）：逐局记录 + 终局聚合判定。</summary>
    public sealed class NetworkSoakReport
    {
        /// <summary>构造报告（逐局记录按完成顺序）。</summary>
        public NetworkSoakReport(IReadOnlyList<NetworkSoakMatchRecord> matches)
        {
            Matches = matches;
        }

        /// <summary>逐局记录。</summary>
        public IReadOnlyList<NetworkSoakMatchRecord> Matches { get; }

        /// <summary>终局对局数。</summary>
        public int FinishedCount => Matches.Count(m => m.Finished);

        /// <summary>是否全部对局正常终局且无失败记录（空批视为未完成）。</summary>
        public bool AllFinished => Matches.Count > 0 && Matches.All(m => m.Finished && m.Failure is null);
    }
}
