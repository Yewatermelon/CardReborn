using System;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>对局终局结果（M3-T7）：不可变，供事件/日志/UI 使用。</summary>
    public sealed class MatchOutcome
    {
        public MatchOutcome(
            string id,
            MatchResult result,
            int? winnerId,
            int turnNumber,
            string reason,
            int startTick,
            int endTick)
        {
            Guard.NotNullOrWhiteSpace(id, nameof(id));
            Guard.NotNullOrWhiteSpace(reason, nameof(reason));
            MatchId = id;
            Result = result;
            WinnerId = winnerId;
            TurnNumber = turnNumber;
            Reason = reason;
            StartTick = startTick;
            EndTick = endTick;
        }

        /// <summary>对局唯一标识（guid 字符串）。</summary>
        public string MatchId { get; }

        /// <summary>终局结果。</summary>
        public MatchResult Result { get; }

        /// <summary>获胜者座位 ID；平局或进行中为 null。</summary>
        public int? WinnerId { get; }

        /// <summary>终局时的回合数。</summary>
        public int TurnNumber { get; }

        /// <summary>结束原因（人类可读）。</summary>
        public string Reason { get; }

        /// <summary>对局开始时的权威 tick。</summary>
        public int StartTick { get; }

        /// <summary>对局结束时的权威 tick。</summary>
        public int EndTick { get; }

        /// <summary>对局持续 tick 数（EndTick - StartTick，负值钳为 0）。</summary>
        public int DurationTicks => Math.Max(0, EndTick - StartTick);
    }
}
