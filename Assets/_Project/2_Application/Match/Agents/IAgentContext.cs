using Card.Domain.Match;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// 决策上下文（M7-T1）：<see cref="IPlayerAgent"/> 激活期内观察对局与上行意图的唯一通道。
    /// <see cref="View"/> 是权威状态的零拷贝只读活视图（M5-T8），
    /// agent 无法经它拿到可写状态；<see cref="Submit"/> 与权威
    /// <c>MatchController.Submit</c> 结果逐位一致，runner 只附加回合激活路由。
    /// </summary>
    public interface IAgentContext
    {
        /// <summary>当前行动方座位（权威结算后的实时值）。</summary>
        int ActivePlayerId { get; }

        /// <summary>权威对局的只读视图（同一活实例，提交后立即反映新状态）。</summary>
        IReadOnlyMatchState View { get; }

        /// <summary>
        /// 上行一条命令：先经 RuleEngine 校验，通过才结算；
        /// 非法命令原样返回 <see cref="CommandResult"/>（含错误码），状态与激活均不变。
        /// 不允许抛异常表达"非法"——非法是正常决策反馈。
        /// </summary>
        CommandResult Submit(IGameCommand command);
    }
}
