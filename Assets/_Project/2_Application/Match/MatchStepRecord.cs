using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 控制器步骤台账单条记录（M4-T2）：每处理一条命令产生一条，**被拒绝也记录**。
    /// 注意：这是"命令处理流水"，不是 M4-T3 的 GameEvent 富事件家族——
    /// 伤害/治疗/死亡等领域事件将在 M4-T3 建模并接入本控制器。
    /// </summary>
    public readonly struct MatchStepRecord
    {
        public MatchStepRecord(
            int sequence,
            string commandType,
            int playerId,
            bool accepted,
            CommandError error,
            TurnPhase phaseAfter,
            bool matchFinished)
        {
            Sequence = sequence;
            CommandType = commandType;
            PlayerId = playerId;
            Accepted = accepted;
            Error = error;
            PhaseAfter = phaseAfter;
            MatchFinished = matchFinished;
        }

        /// <summary>从 0 起每处理一条 +1（含被拒命令）。</summary>
        public int Sequence { get; }

        /// <summary>命令具体类型名（如 <c>EndTurnCommand</c>）。</summary>
        public string CommandType { get; }

        /// <summary>发起座位 Id。</summary>
        public int PlayerId { get; }

        /// <summary>true=校验通过并已结算；false=被 RuleEngine 拒绝（状态零变更）。</summary>
        public bool Accepted { get; }

        /// <summary>拒绝错误码；接受时为 <see cref="CommandError.None"/>。</summary>
        public CommandError Error { get; }

        /// <summary>处理后的阶段（终局那一步为 MatchEnd，其余 M4-T2 为 Main）。</summary>
        public TurnPhase PhaseAfter { get; }

        /// <summary>该步结算后对局终局（最后一条接受记录可为 true）。</summary>
        public bool MatchFinished { get; }
    }
}
