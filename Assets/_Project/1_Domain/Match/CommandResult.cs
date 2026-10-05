namespace Card.Domain.Match
{
    /// <summary>
    /// 命令校验失败原因（强类型错误码，M3-T4）。对应网络层
    /// CommandAccepted / CommandRejected 中的拒绝原因（见 Docs/05 §9.1）。
    /// 新增成员只追加，不改变既有值（上行协议兼容）。
    /// </summary>
    public enum CommandError
    {
        /// <summary>无错误（校验通过）。</summary>
        None = 0,

        /// <summary>未知/不支持的命令类型。</summary>
        UnknownCommand,

        /// <summary>非该玩家回合或命令归属玩家不匹配。</summary>
        NotYourTurn,

        /// <summary>法力不足。</summary>
        NotEnoughMana,

        /// <summary>战场已满（超过场面上限）。</summary>
        BoardFull,

        /// <summary>需要目标而未提供目标。</summary>
        TargetRequired,

        /// <summary>目标非法（不存在/已死亡/错误类型/不满足条件）。</summary>
        InvalidTarget,

        /// <summary>要打出的牌不在发起者手牌中。</summary>
        CardNotInHand,

        /// <summary>攻击者不在发起者场上。</summary>
        AttackerNotOnBoard,

        /// <summary>攻击者处于召唤失调（当回合进场且无冲锋/突袭）。</summary>
        SummoningSickness,

        /// <summary>攻击者本回合攻击次数已用完。</summary>
        AlreadyAttacked,

        /// <summary>敌方存在嘲讽随从，必须优先攻击嘲讽目标。</summary>
        MustTargetTaunt,

        /// <summary>英雄技能本回合已使用。</summary>
        HeroPowerAlreadyUsed
    }

    /// <summary>
    /// 命令校验结果（不可变值对象，M3-T4）：有效，或失败并携带
    /// <see cref="CommandError"/> 与可选 <see cref="Detail"/>。由 RuleEngine.Validate 产出。
    /// </summary>
    public readonly struct CommandResult
    {
        private readonly CommandError _error;
        private readonly string? _detail;

        private CommandResult(CommandError error, string? detail)
        {
            _error = error;
            _detail = detail;
        }

        public bool IsValid
        {
            get { return _error == CommandError.None; }
        }

        public bool IsInvalid
        {
            get { return _error != CommandError.None; }
        }

        public CommandError Error
        {
            get { return _error; }
        }

        /// <summary>失败原因的补充说明；无说明时为空字符串。</summary>
        public string Detail
        {
            get { return _detail ?? string.Empty; }
        }

        /// <summary>校验通过。</summary>
        public static CommandResult Valid()
        {
            return new CommandResult(CommandError.None, string.Empty);
        }

        /// <summary>校验失败：携带错误码与可选补充说明。</summary>
        public static CommandResult Invalid(CommandError error, string? detail = null)
        {
            return new CommandResult(error, detail ?? string.Empty);
        }
    }
}
