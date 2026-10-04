using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 单个状态的进入 / 退出回调。阶段内的业务由处理器自己依赖的服务完成
    /// （依赖通过构造函数注入，见 Docs/03 第 5.5、5.7 节）。
    /// </summary>
    public interface IStateHandler<TState>
    {
        /// <summary>进入该状态后调用。</summary>
        void Enter();

        /// <summary>离开该状态前调用；用于清理临时状态。</summary>
        void Exit();
    }

    /// <summary>
    /// 通用状态机：状态与合法转移都显式注册，非法转移返回失败而不是静默忽略。
    ///
    /// 约定：
    /// 1. <typeparamref name="TState"/> 建议使用枚举或不可变值类型；
    /// 2. 只有显式声明过的边才能转移——<b>未声明的自转移（A → A）同样被拒绝</b>，需要时显式声明；
    /// 3. 合法转移的回调顺序固定为 <c>Exit(旧状态)</c> → <c>Enter(新状态)</c>；
    /// 4. 非法转移不改变 <see cref="Current"/>，也不触发任何回调；
    /// 5. 回调抛异常会向上传播（不吞异常、不回滚），此时 <see cref="Current"/> 已是目标状态；
    /// 6. <b>非线程安全</b>：单线程使用（服务器 tick / 主线程）。
    /// </summary>
    public sealed class StateMachine<TState>
    {
        /// <summary>目标状态未注册时的错误码。</summary>
        public const string ErrorUnknownState = "ERROR_STATE_UNKNOWN";

        /// <summary>当前状态到目标状态之间没有声明合法边时的错误码。</summary>
        public const string ErrorIllegalTransition = "ERROR_STATE_ILLEGAL_TRANSITION";

        private readonly Dictionary<TState, IStateHandler<TState>?> _handlers =
            new Dictionary<TState, IStateHandler<TState>?>();

        private readonly HashSet<Transition> _allowedTransitions = new HashSet<Transition>();

        /// <summary>创建状态机；可选地直接为初始状态绑定处理器（否则需自行 <see cref="Register"/>）。</summary>
        public StateMachine(TState initialState, IStateHandler<TState>? initialHandler = null)
        {
            Current = initialState;

            if (initialHandler != null)
            {
                Register(initialState, initialHandler);
            }
        }

        /// <summary>当前状态。</summary>
        public TState Current { get; private set; }

        /// <summary>注册状态（可同时绑定处理器）。重复注册属配置错误，抛 <see cref="ArgumentException"/>。</summary>
        public void Register(TState state, IStateHandler<TState>? handler = null)
        {
            ThrowIfNullState(state, nameof(state));

            if (_handlers.ContainsKey(state))
            {
                throw new ArgumentException("状态已注册：" + Convert.ToString(state), nameof(state));
            }

            _handlers.Add(state, handler);
        }

        /// <summary>声明一条合法转移边。端点必须已注册；重复声明同一条边是幂等的。</summary>
        public void AllowTransition(TState from, TState to)
        {
            ThrowIfNullState(from, nameof(from));
            ThrowIfNullState(to, nameof(to));

            if (!_handlers.ContainsKey(from))
            {
                throw new ArgumentException("起点状态未注册：" + Convert.ToString(from), nameof(from));
            }

            if (!_handlers.ContainsKey(to))
            {
                throw new ArgumentException("终点状态未注册：" + Convert.ToString(to), nameof(to));
            }

            _allowedTransitions.Add(new Transition(from, to));
        }

        /// <summary>状态是否已注册。</summary>
        public bool IsRegistered(TState state)
        {
            return !IsNullState(state) && _handlers.ContainsKey(state);
        }

        /// <summary>无副作用地查询能否转移到目标状态（供 Application 层决策）。</summary>
        public bool CanTransitionTo(TState target)
        {
            return IsRegistered(target) && _allowedTransitions.Contains(new Transition(Current, target));
        }

        /// <summary>
        /// 转移到目标状态：合法则先 Exit 旧状态、再 Enter 新状态并返回成功；
        /// 非法则返回失败且不改变状态、不触发回调。
        /// </summary>
        public Result TransitionTo(TState target)
        {
            if (!IsRegistered(target))
            {
                return Result.Failure(
                    ErrorUnknownState,
                    "目标状态未注册：" + Convert.ToString(target));
            }

            if (!_allowedTransitions.Contains(new Transition(Current, target)))
            {
                return Result.Failure(
                    ErrorIllegalTransition,
                    "非法转移：" + Convert.ToString(Current) + " → " + Convert.ToString(target));
            }

            TState previous = Current;
            Current = target;
            GetHandler(previous)?.Exit();
            GetHandler(target)?.Enter();
            return Result.Success();
        }

        private IStateHandler<TState>? GetHandler(TState state)
        {
            return _handlers.TryGetValue(state, out IStateHandler<TState>? handler) ? handler : null;
        }

        private static bool IsNullState(TState state)
        {
            // 值类型状态不可能为 null，且此分支不会被取到，因此不会产生装箱。
            if (typeof(TState).IsValueType)
            {
                return false;
            }

            object? boxed = state;
            return boxed == null;
        }

        private static void ThrowIfNullState(TState state, string paramName)
        {
            if (IsNullState(state))
            {
                throw new ArgumentNullException(paramName, "状态不能为 null。");
            }
        }

        /// <summary>转移边：无状态集合的键值载体，只用于 HashSet 判定。</summary>
        private readonly struct Transition : IEquatable<Transition>
        {
            private readonly TState _from;
            private readonly TState _to;

            public Transition(TState from, TState to)
            {
                _from = from;
                _to = to;
            }

            public bool Equals(Transition other)
            {
                return EqualityComparer<TState>.Default.Equals(_from, other._from)
                    && EqualityComparer<TState>.Default.Equals(_to, other._to);
            }

            public override bool Equals(object obj)
            {
                return obj is Transition other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EqualityComparer<TState>.Default.GetHashCode(_from) * 397)
                        ^ EqualityComparer<TState>.Default.GetHashCode(_to);
                }
            }
        }
    }
}
