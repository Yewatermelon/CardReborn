using System;
using Card.Core;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// 回合终止保障选项（M7-T3，FR-6.4）：三层上限互相独立，任一触发即终止本回合决策。
    /// 均为构造级算法参数（非 Excel 配置），默认值与 M7-T2 硬上限语义一致。
    /// </summary>
    public sealed class TurnGuardOptions
    {
        /// <summary>步数上限：单次激活内提交数（含被拒）。默认 500（接管 M7-T2 硬上限）。</summary>
        public int MaxSteps { get; init; } = 500;

        /// <summary>时间上限：激活内允许跨越的 <see cref="IClock"/> tick 预算；0 = 不启用（默认）。</summary>
        public int MaxTicks { get; init; } = 0;

        /// <summary>无进展阈值：连续 N 步提交后局面签名无变化即触发。默认 8。</summary>
        public int NoProgressLimit { get; init; } = 8;

        /// <summary>默认配置（MaxSteps=500 / MaxTicks=0 / NoProgressLimit=8）。</summary>
        public static TurnGuardOptions Default { get; } = new TurnGuardOptions();

        public const string ReasonStepLimit = "step-limit";
        public const string ReasonTimeLimit = "time-limit";
        public const string ReasonNoProgress = "no-progress";
    }

    /// <summary>
    /// 回合守卫（M7-T3，FR-6.4）：agent 决策循环每提交一条命令后 <see cref="RegisterStep"/> 一次，
    /// 三层上限（步数 / 时间 / 无进展）任一耗尽即 <see cref="IsExhausted"/>；
    /// agent 收到后停止决策并必发 EndTurn——EndTurn 不受守卫约束，保证"任何局面下 AI 回合必然结束"。
    /// 职责单一：只计数与判定，局面签名由调用方生成传入（不依赖 MatchState 类型）。
    /// </summary>
    public sealed class TurnGuard
    {
        private readonly TurnGuardOptions _options;
        private readonly IClock? _clock;
        private int _startTick;
        private int _steps;
        private int _noProgress;
        private string? _lastSignature;
        private string? _exhaustReason;

        /// <summary>构造。<paramref name="options"/> 为 null 用默认；MaxTicks &gt; 0 时必须提供 clock。</summary>
        public TurnGuard(TurnGuardOptions? options = null, IClock? clock = null)
        {
            _options = options ?? TurnGuardOptions.Default;
            if (_options.MaxSteps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), _options.MaxSteps, "MaxSteps 必须 ≥ 1。");
            }

            if (_options.MaxTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), _options.MaxTicks, "MaxTicks 必须 ≥ 0（0 = 不启用）。");
            }

            if (_options.NoProgressLimit < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), _options.NoProgressLimit, "NoProgressLimit 必须 ≥ 1。");
            }

            if (_options.MaxTicks > 0 && clock == null)
            {
                // 防呆：要启用时间上限就必须给时钟，禁止静默不生效（Fail fast）。
                throw new ArgumentException("MaxTicks > 0 时必须提供 IClock。", nameof(clock));
            }

            _clock = clock;
            _startTick = _clock?.CurrentTick ?? 0;
        }

        /// <summary>本激活已注册步数（含被拒命令）。</summary>
        public int Steps => _steps;

        /// <summary>当前连续无进展步数。</summary>
        public int NoProgressSteps => _noProgress;

        /// <summary>任一上限已触发（此后 RegisterStep 幂等）。</summary>
        public bool IsExhausted => _exhaustReason != null;

        /// <summary>触发原因（TurnGuardOptions.Reason* 之一）；未触发为 null。</summary>
        public string? ExhaustReason => _exhaustReason;

        /// <summary>每次激活开始调用：重置全部计数并重记起始 tick（幂等，跨回合可复用）。</summary>
        public void OnActivationStarted()
        {
            _steps = 0;
            _noProgress = 0;
            _lastSignature = null;
            _exhaustReason = null;
            _startTick = _clock?.CurrentTick ?? 0;
        }

        /// <summary>
        /// 每条已提交命令（含被拒）后调用：<paramref name="signature"/> 为提交后的局面签名（调用方生成）。
        /// 按"步数 → 时间 → 无进展"顺序判定，任一触发即 Exhausted（此后调用幂等）。
        /// </summary>
        public void RegisterStep(string signature)
        {
            if (IsExhausted)
            {
                return;
            }

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            _steps++;
            if (_steps >= _options.MaxSteps)
            {
                _exhaustReason = TurnGuardOptions.ReasonStepLimit;
                return;
            }

            if (_options.MaxTicks > 0 && _clock!.CurrentTick - _startTick >= _options.MaxTicks)
            {
                _exhaustReason = TurnGuardOptions.ReasonTimeLimit;
                return;
            }

            if (_lastSignature != null && _lastSignature == signature)
            {
                _noProgress++;
            }
            else
            {
                _noProgress = 0;
            }

            _lastSignature = signature;
            if (_noProgress >= _options.NoProgressLimit)
            {
                _exhaustReason = TurnGuardOptions.ReasonNoProgress;
            }
        }
    }
}
