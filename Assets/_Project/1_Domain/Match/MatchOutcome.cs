namespace Card.Domain.Match
{
    /// <summary>对局终局结果（M3-T7）：不可变，供事件/日志/UI 使用。</summary>
    public sealed class MatchOutcome
    {
        public MatchOutcome(MatchResult result, int? winnerId, int turnNumber, string reason)
        {
            Result = result;
            WinnerId = winnerId;
            TurnNumber = turnNumber;
            Reason = reason;
        }

        /// <summary>终局结果。</summary>
        public MatchResult Result { get; }

        /// <summary>获胜者座位 ID；平局或进行中为 null。</summary>
        public int? WinnerId { get; }

        /// <summary>终局时的回合数。</summary>
        public int TurnNumber { get; }

        /// <summary>结束原因（人类可读）。</summary>
        public string Reason { get; }
    }
}
