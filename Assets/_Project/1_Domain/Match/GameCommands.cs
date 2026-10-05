namespace Card.Domain.Match
{
    /// <summary>
    /// GameCommand 家族（M3-T4）：玩家在 Main 阶段全部意图的不可变值对象。
    /// 构造函数只赋值、不做业务校验——任何命令（含越权/伪造）都可构造，
    /// 语义合法性全部由权威侧 RuleEngine 校验后拒绝（铁律 5、12）。
    /// </summary>

    /// <summary>出一张手牌：<see cref="CardInstanceId"/> 指向手牌实例；<see cref="Target"/> 可为 None。</summary>
    public readonly struct PlayCardCommand : IGameCommand
    {
        public PlayCardCommand(int playerId, int cardInstanceId, TargetRef target)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            Target = target;
        }

        public int PlayerId { get; }

        /// <summary>要打出的手牌实例 Id（在手牌分区中按 Id 查询）。</summary>
        public int CardInstanceId { get; }

        public TargetRef Target { get; }
    }

    /// <summary>我方场上随从攻击：<see cref="AttackerInstanceId"/> 指向己方随从；<see cref="Target"/> 为随从或英雄。</summary>
    public readonly struct AttackCommand : IGameCommand
    {
        public AttackCommand(int playerId, int attackerInstanceId, TargetRef target)
        {
            PlayerId = playerId;
            AttackerInstanceId = attackerInstanceId;
            Target = target;
        }

        public int PlayerId { get; }

        /// <summary>发起攻击的己方场上随从实例 Id。</summary>
        public int AttackerInstanceId { get; }

        public TargetRef Target { get; }
    }

    /// <summary>使用英雄技能：<see cref="Target"/> 可为 None 或角色（如火冲需指定目标）。</summary>
    public readonly struct UseHeroPowerCommand : IGameCommand
    {
        public UseHeroPowerCommand(int playerId, TargetRef target)
        {
            PlayerId = playerId;
            Target = target;
        }

        public int PlayerId { get; }

        public TargetRef Target { get; }
    }

    /// <summary>主动结束当前回合（无目标、无额外字段）。</summary>
    public readonly struct EndTurnCommand : IGameCommand
    {
        public EndTurnCommand(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }
}
