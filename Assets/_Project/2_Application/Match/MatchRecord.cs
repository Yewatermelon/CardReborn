using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 整局对局记录（M14-T5）：聚合 <see cref="MatchOutcome"/>（终局元数据）
    /// + <see cref="MatchStepRecord"/>[]（命令流水）+ 玩家标识 + 网络结束原因。
    /// 可序列化往返，供本地持久化与复盘使用。
    /// </summary>
    public sealed class MatchRecord
    {
        public MatchRecord(
            MatchOutcome outcome,
            IReadOnlyList<MatchStepRecord> steps,
            string[] playerNames,
            string? endReasonNet)
        {
            Guard.NotNull(outcome, nameof(outcome));
            Guard.NotNull(steps, nameof(steps));
            Guard.NotNull(playerNames, nameof(playerNames));
            Outcome = outcome;
            Steps = steps;
            PlayerNames = playerNames;
            EndReasonNet = endReasonNet;
        }

        /// <summary>终局元数据（含 MatchId / 时长 tick / 回合数 / 胜负）。</summary>
        public MatchOutcome Outcome { get; }

        /// <summary>命令处理流水（含被拒命令），按处理顺序。</summary>
        public IReadOnlyList<MatchStepRecord> Steps { get; }

        /// <summary>座位 0/1 玩家标识（如 "座0-HERO_MAGE"）。</summary>
        public string[] PlayerNames { get; }

        /// <summary>网络层结束原因（Normal / Disconnect / HostQuit）；PVE 或未联网为 null。</summary>
        public string? EndReasonNet { get; }
    }
}
