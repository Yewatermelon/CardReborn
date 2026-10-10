using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 命令信封（M11-T2）：命令 + 元信息。M11 只保留字段，严格校验（序号/令牌/回合归属）在 M13。
    /// </summary>
    public sealed class CommandEnvelope
    {
        /// <summary>客户端递增序号（M11 只透传，M13 做幂等去重）。</summary>
        public ulong CommandSequence { get; }

        /// <summary>会话令牌（M11 可为 null——PVE 无会话；M13 做会话校验）。</summary>
        public string SessionToken { get; }

        /// <summary>目标回合号（M11 只透传，M13 检测过期命令）。</summary>
        public int TargetTurnNumber { get; }

        /// <summary>实际命令内容。</summary>
        public IGameCommand Command { get; }

        public CommandEnvelope(
            ulong commandSequence,
            string sessionToken,
            int targetTurnNumber,
            IGameCommand command)
        {
            CommandSequence = commandSequence;
            SessionToken = sessionToken;
            TargetTurnNumber = targetTurnNumber;
            Command = command;
        }
    }
}
