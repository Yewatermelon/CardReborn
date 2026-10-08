using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// Agent 装配与回合激活路由（M7-T1）：把两个座位的 <see cref="IPlayerAgent"/>
    /// 绑到权威 <see cref="ICommandAuthority"/> 上。本类同时是权威入口的装饰器
    /// （UI 经 ICommandSink 与 AI 经 IAgentContext 走同一个 <see cref="Submit"/>）
    /// 与传给 agent 的 <see cref="IAgentContext"/>：只做激活路由，不校验、不改命令、
    /// 不缓存状态副本。单线程使用（权威宿主模型）。
    ///
    /// 路由规则：每次 accepted 提交后以权威侧 <c>ActivePlayerId</c>/<c>IsFinished</c>
    /// 为准同步——座位变化则固定按"旧 <see cref="IPlayerAgent.OnTurnDeactivated"/>
    /// → 新 <see cref="IPlayerAgent.OnTurnActivated"/>"回调；终局只停活当前座位；
    /// 被拒命令不触发路由。AI 在激活回调内提交结束回合会同步重入激活对手，
    /// 因而一整局可在一次 <see cref="Start"/> 调用栈内跑完。
    /// </summary>
    public sealed class AgentMatchRunner : ICommandAuthority, IAgentContext
    {
        private const int NoActiveSeat = -1;

        private readonly ICommandAuthority _authority;
        private readonly IReadOnlyMatchState _view;
        private readonly IPlayerAgent?[] _agents = new IPlayerAgent?[2];
        private int _activeSeat = NoActiveSeat;

        public AgentMatchRunner(
            ICommandAuthority authority,
            IReadOnlyMatchState view,
            IReadOnlyList<IPlayerAgent> agents)
        {
            _authority = Guard.NotNull(authority, nameof(authority));
            _view = Guard.NotNull(view, nameof(view));
            Guard.NotNull(agents, nameof(agents));

            if (agents.Count != 2)
            {
                throw new ArgumentException(
                    "AgentMatchRunner 必须装配恰好 2 个座位的 agent，实际数量：" + agents.Count,
                    nameof(agents));
            }

            for (int i = 0; i < agents.Count; i++)
            {
                IPlayerAgent agent = agents[i]
                    ?? throw new ArgumentNullException(nameof(agents), "agents[" + i + "] 为 null。");
                int seat = agent.PlayerId;
                if (seat < 0 || seat >= _agents.Length)
                {
                    throw new ArgumentException(
                        "agent 座位 Id 必须为 0 或 1，实际：" + seat, nameof(agents));
                }

                if (_agents[seat] != null)
                {
                    throw new ArgumentException(
                        "座位 " + seat + " 绑定了重复的 agent。", nameof(agents));
                }

                _agents[seat] = agent;
            }
        }

        /// <inheritdoc />
        public int ActivePlayerId => _view.ActivePlayerId;

        /// <inheritdoc />
        public IReadOnlyMatchState View => _view;

        /// <summary>
        /// 激活当前行动方开始决策；重复调用幂等（不产生重复回调）。
        /// 若对局已终局则不激活任何 agent。
        /// </summary>
        public void Start()
        {
            Pump();
        }

        /// <summary>
        /// 经权威提交一条命令；accepted 后立即重同步激活态。
        /// 返回权威侧原始 <see cref="CommandResult"/>，不包装、不吞异常。
        /// </summary>
        public CommandResult Submit(IGameCommand command)
        {
            CommandResult result = _authority.Submit(command);
            if (result.IsValid)
            {
                Pump();
            }

            return result;
        }

        /// <summary>
        /// 按权威现状重同步激活态：终局停活；座位切换则先停旧后启新；未变化不动。
        /// 供未走本 runner 的外部权威提交（如直接调 MatchController.Submit）后补路由。
        /// </summary>
        public void Pump()
        {
            if (_view.IsFinished)
            {
                DeactivateActive();
                return;
            }

            int seat = _view.ActivePlayerId;
            if (seat == _activeSeat)
            {
                return;
            }

            DeactivateActive();
            _activeSeat = seat;
            _agents[seat]!.OnTurnActivated(this);
        }

        private void DeactivateActive()
        {
            if (_activeSeat == NoActiveSeat)
            {
                return;
            }

            int seat = _activeSeat;
            _activeSeat = NoActiveSeat;
            _agents[seat]!.OnTurnDeactivated();
        }
    }
}
