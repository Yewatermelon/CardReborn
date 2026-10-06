using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 回合阶段流转（M4-T1；Docs/01 §3.2）。
    ///
    /// 以 <see cref="StateMachine{TState}"/> 为内核声明六个阶段与合法边：
    /// 正常顺序 <c>MatchStart → TurnStart → Draw → Main → TurnEnd → TurnStart</c>
    /// （TurnEnd 回到 TurnStart 构成回合回环）；任意非终局阶段可进入
    /// <c>MatchEnd</c>，MatchEnd 无出边。
    ///
    /// 本类型只负责"阶段合法性"：成功转移后同步写回 <see cref="MatchState.Phase"/>；
    /// 不含任何阶段业务（加/回水晶、抽牌、攻击次数重置、行动方切换、触发效果分别归
    /// M4-T8 / CardDrawService / M4-T2 控制器 / M4-T5）。非线程安全（主线程单线程使用）。
    /// </summary>
    public sealed class TurnStateMachine
    {
        /// <summary>
        /// 正常后继表：阶段严格顺序的唯一真相；<see cref="TurnPhase.MatchEnd"/>
        /// 不在表中（终局，无后继）。
        /// </summary>
        private static readonly Dictionary<TurnPhase, TurnPhase> Successors =
            new Dictionary<TurnPhase, TurnPhase>
            {
                [TurnPhase.MatchStart] = TurnPhase.TurnStart,
                [TurnPhase.TurnStart] = TurnPhase.Draw,
                [TurnPhase.Draw] = TurnPhase.Main,
                [TurnPhase.Main] = TurnPhase.TurnEnd,
                [TurnPhase.TurnEnd] = TurnPhase.TurnStart
            };

        private readonly MatchState _state;
        private readonly StateMachine<TurnPhase> _machine;

        /// <summary>
        /// 以既有对局状态的当前阶段构造：注册全部六阶段与合法边。
        /// <see cref="MatchFactory"/> 直接产出 Main 阶段对局，可直接接入。
        /// </summary>
        public TurnStateMachine(MatchState state)
        {
            Guard.NotNull(state, nameof(state));
            Guard.Require(
                Enum.IsDefined(typeof(TurnPhase), state.Phase),
                nameof(state),
                "MatchState.Phase 不是已定义的 TurnPhase。");

            _state = state;
            _machine = new StateMachine<TurnPhase>(state.Phase);
            RegisterPhases();
            RegisterTransitions();
        }

        /// <summary>机器当前阶段；成功转移后与 <see cref="MatchState.Phase"/> 一致。</summary>
        public TurnPhase CurrentPhase => _machine.Current;

        /// <summary>无副作用查询当前是否存在到目标阶段的合法边。</summary>
        public bool CanMoveTo(TurnPhase target)
        {
            return _machine.CanTransitionTo(target);
        }

        /// <summary>
        /// 按声明边转移：合法则先经内核状态机转移，成功后同步写回对局状态；
        /// 非法返回失败（<c>ERROR_STATE_ILLEGAL_TRANSITION</c>），机器与对局状态均不变。
        /// </summary>
        public Result MoveTo(TurnPhase target)
        {
            Result result = _machine.TransitionTo(target);
            if (result.IsSuccess)
            {
                _state.Phase = target;
            }

            return result;
        }

        /// <summary>
        /// 沿正常顺序推进到唯一后继（<see cref="TurnPhase.TurnEnd"/> 回到
        /// <see cref="TurnPhase.TurnStart"/>）；在 <see cref="TurnPhase.MatchEnd"/>
        /// 调用返回失败且不改变状态。
        /// </summary>
        public Result Advance()
        {
            if (!Successors.TryGetValue(_machine.Current, out TurnPhase next))
            {
                return Result.Failure(
                    StateMachine<TurnPhase>.ErrorIllegalTransition,
                    "终局阶段 MatchEnd 之后没有可推进的阶段。");
            }

            return MoveTo(next);
        }

        /// <summary>
        /// 进入终局阶段（任意非终局阶段合法）。终局判定本身归
        /// <c>MatchEvaluator</c>，本方法只做阶段流转。
        /// </summary>
        public Result EndMatch()
        {
            return MoveTo(TurnPhase.MatchEnd);
        }

        private void RegisterPhases()
        {
            foreach (TurnPhase phase in Enum.GetValues(typeof(TurnPhase)))
            {
                _machine.Register(phase);
            }
        }

        private void RegisterTransitions()
        {
            foreach (KeyValuePair<TurnPhase, TurnPhase> edge in Successors)
            {
                _machine.AllowTransition(edge.Key, edge.Value);
                _machine.AllowTransition(edge.Key, TurnPhase.MatchEnd);
            }
        }
    }
}
