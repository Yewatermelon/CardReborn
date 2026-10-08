namespace Card.Application.Match.Agents
{
    /// <summary>
    /// 对局决策者（M7-T1，FR-6.1）：人类玩家与 AI 的统一接口。
    /// 实现只表达"我要做什么"（产出 <see cref="Domain.Match.IGameCommand"/>），
    /// 不读取可写状态、不直接修改对局；命令一律经 <see cref="IAgentContext.Submit"/>
    /// 上行，由权威侧 RuleEngine 校验后才生效（铁律 5、FR-6.5）。
    /// 人类实现：激活时启用 UI 输入，点击回调中提交命令；
    /// AI 实现（M7-T2）：激活回调内按算法同步产出并提交命令。
    /// </summary>
    public interface IPlayerAgent
    {
        /// <summary>该决策者绑定的座位 Id（对局中固定为 0 或 1）。</summary>
        int PlayerId { get; }

        /// <summary>
        /// 成为行动方时由 <see cref="AgentMatchRunner"/> 调用一次，直至停活。
        /// 上下文仅在本次激活期内有效；人类实现在此启用输入，
        /// AI 实现可在回调内连续提交直至结束回合（回调内重入合法）。
        /// </summary>
        void OnTurnActivated(IAgentContext context);

        /// <summary>
        /// 行动权转移到其他座位或对局终局时调用，与每次激活配对。
        /// 人类实现在此停活输入；实现必须幂等容忍重复调用。
        /// </summary>
        void OnTurnDeactivated();
    }
}
