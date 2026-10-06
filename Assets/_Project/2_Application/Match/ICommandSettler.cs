using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 命令结算器接缝（M4-T2）：通过 <see cref="RuleEngine"/> 校验后的命令在此产生实际状态变更。
    /// "输入（命令）与结算解耦"——出牌/攻击/技能结算器在 M4-T4/T6 实现并向
    /// <see cref="MatchController"/> 注册；本任务只有 <see cref="EndTurnSettler"/>。
    /// 实现只允许修改 <paramref name="context"/> 提供的对局状态；不做终局判定
    /// （终局由控制器在结算后统一调用 <see cref="MatchEvaluator"/>，终局入口唯一）。
    /// </summary>
    public interface ICommandSettler
    {
        /// <summary>是否结算该命令类型（先注册者优先匹配）。</summary>
        bool CanSettle(IGameCommand command);

        /// <summary>执行结算：前置条件是命令已通过权威校验；实现内部不再重复校验。</summary>
        void Settle(SettlementContext context, IGameCommand command);
    }

    /// <summary>
    /// 结算上下文：向结算器暴露当前对局、配置数据库与回合阶段机。
    /// <see cref="CardDrawService"/> 等为既有静态服务，不在此注入。
    /// </summary>
    public sealed class SettlementContext
    {
        public SettlementContext(MatchState state, CardDatabase database, TurnStateMachine phases, EventLog events)
        {
            State = Guard.NotNull(state, nameof(state));
            Database = Guard.NotNull(database, nameof(database));
            Phases = Guard.NotNull(phases, nameof(phases));
            Events = Guard.NotNull(events, nameof(events));
        }

        /// <summary>当前权威对局状态。</summary>
        public MatchState State { get; }

        /// <summary>配置数据库（卡牌/英雄/规则表只读源）。</summary>
        public CardDatabase Database { get; }

        /// <summary>回合阶段机（与 <see cref="State"/> 同步）。</summary>
        public TurnStateMachine Phases { get; }

        /// <summary>事件接收器：结算器产出的状态变更事件由此收集。</summary>
        public EventLog Events { get; }
    }
}
