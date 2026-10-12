using System;
using System.Collections.Generic;
using System.Globalization;
using Card.Application.Match.Effects;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 对局控制器（M4-T2 ★）：权威侧命令驱动入口。FIFO 命令队列按
    /// "校验（<see cref="RuleEngine"/>）→ 结算（<see cref="ICommandSettler"/>）→
    /// 终局判定（<see cref="MatchEvaluator"/>）→ 步骤台账"流水线同步推进。
    /// 终局判定只在本入口发生一次（结算器不写终局）；终局后队列停止处理，
    /// 后续命令由 RuleEngine 拒绝。单线程使用（权威宿主模型，Docs/05）。
    /// </summary>
    public sealed class MatchController : ICommandAuthority
    {
        private readonly MatchState _state;
        private readonly CardDatabase _database;
        private readonly TurnStateMachine _phases;
        private readonly Queue<IGameCommand> _pending = new Queue<IGameCommand>();
        private readonly List<ICommandSettler> _settlers = new List<ICommandSettler>();
        private readonly List<MatchStepRecord> _history = new List<MatchStepRecord>();
        private readonly EventLog _events = new EventLog();
        private readonly TriggerDispatcher _dispatcher = new TriggerDispatcher();
        private readonly DeathProcessor _deaths = new DeathProcessor();

        // M14-T5：对局元数据（构造时生成/记录）
        private readonly string _matchId;
        private readonly int _startTick;
        private readonly IClock? _clock;

        public MatchController(MatchState state, CardDatabase database, IClock? clock = null)
        {
            _state = Guard.NotNull(state, nameof(state));
            _database = Guard.NotNull(database, nameof(database));
            _clock = clock;
            _startTick = clock?.CurrentTick ?? 0;
            _matchId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            _phases = new TurnStateMachine(state);
            RegisterSettler(new EndTurnSettler());
            RegisterSettler(new PlayCardSettler());
            RegisterSettler(new AttackSettler());
            RegisterSettler(new HeroPowerSettler());
            RegisterSettler(new ForfeitSettler());
        }

        /// <summary>对局唯一标识（guid 字符串）。</summary>
        public string MatchId => _matchId;

        /// <summary>当前权威对局状态。</summary>
        public MatchState State => _state;

        /// <summary>
        /// 只读对局视图（M5-T8 ★）：表现层/PVP 客户端读取对局的唯一入口，
        /// 零拷贝活视图，实时映射权威状态，类型层面不可写。
        /// </summary>
        public IReadOnlyMatchState View => _state;

        /// <summary>当前回合阶段（与 <see cref="State"/> 的 Phase 同步）。</summary>
        public TurnPhase Phase => _phases.CurrentPhase;

        /// <summary>对局是否已终局。</summary>
        public bool IsFinished => _state.IsFinished;

        /// <summary>已入队尚未处理的命令数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>终局结果；未终局为 null。</summary>
        public MatchOutcome? LastOutcome { get; private set; }

        /// <summary>终局完整记录（含命令流水与玩家标识）；未终局为 null。</summary>
        public MatchRecord? LastRecord { get; private set; }

        /// <summary>命令处理台账（含被拒命令），按处理顺序。</summary>
        public IReadOnlyList<MatchStepRecord> History => _history;

        /// <summary>状态变更事件流（按结算顺序，序号单调）。</summary>
        public IReadOnlyList<GameEvent> Events => _events.Events;

        /// <summary>追加结算器（M4-T4/T6 用）；先注册者优先匹配；null 抛异常。</summary>
        public void RegisterSettler(ICommandSettler settler)
        {
            Guard.NotNull(settler, nameof(settler));
            _settlers.Add(settler);
        }

        /// <summary>命令入队（不立即处理）；null 抛 <see cref="ArgumentNullException"/>。</summary>
        public void Enqueue(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));
            _pending.Enqueue(command);
        }

        /// <summary>入队并立即处理一条；返回该命令的校验/拒绝结果。</summary>
        public CommandResult Submit(IGameCommand command)
        {
            Enqueue(command);
            return ProcessNext();
        }

        /// <summary>
        /// 处理队首一条：校验失败→记台账并原样返回（状态零变更）；
        /// 通过但缺少结算器→抛 <see cref="InvalidOperationException"/>（装配错误）；
        /// 结算后统一终局判定。队列为空时抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        public CommandResult ProcessNext()
        {
            if (_pending.Count == 0)
            {
                throw new InvalidOperationException("命令队列为空，没有可处理的命令。");
            }

            IGameCommand command = _pending.Dequeue();
            CommandResult result = RuleEngine.Validate(_state, command, _database);
            if (result.IsInvalid)
            {
                Record(command, result);
                return result;
            }

            ICommandSettler settler = FindSettler(command)
                ?? throw new InvalidOperationException(
                    "命令已通过校验但缺少结算器（宿主装配错误）：" + command.GetType().Name);

            SettlementContext ctx = new SettlementContext(_state, _database, _phases, _events, _dispatcher);
            settler.Settle(ctx, command);
            _deaths.Process(ctx);
            FinishIfDecided(command);
            return CommandResult.Valid();
        }

        /// <summary>循环处理至队列空或对局终局；返回实际处理条数。</summary>
        public int ProcessPending()
        {
            int processed = 0;
            while (_pending.Count > 0 && !_state.IsFinished)
            {
                ProcessNext();
                processed++;
            }

            return processed;
        }

        private void FinishIfDecided(IGameCommand command)
        {
            MatchOutcome evaluatorOutcome = MatchEvaluator.Evaluate(_state);
            bool finished = evaluatorOutcome.Result != MatchResult.Ongoing;
            if (finished)
            {
                _state.IsFinished = true;
                int endTick = _clock?.CurrentTick ?? _startTick;

                // 用完整数据重新构造 MatchOutcome（替换 evaluator 的 dummy id/tick）
                LastOutcome = new MatchOutcome(
                    _matchId,
                    evaluatorOutcome.Result,
                    evaluatorOutcome.WinnerId,
                    evaluatorOutcome.TurnNumber,
                    evaluatorOutcome.Reason,
                    _startTick,
                    endTick);

                // 组装 MatchRecord（M14-T5）
                string[] playerNames =
                {
                    "座" + _state.GetPlayer(0).Id.ToString(CultureInfo.InvariantCulture)
                        + "-" + _state.GetPlayer(0).Hero.HeroKey,
                    "座" + _state.GetPlayer(1).Id.ToString(CultureInfo.InvariantCulture)
                        + "-" + _state.GetPlayer(1).Hero.HeroKey,
                };
                LastRecord = new MatchRecord(LastOutcome, _history.ToArray(), playerNames, null);

                Result endResult = _phases.EndMatch();
                if (endResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        "终局后阶段流转失败：" + endResult.ErrorCode);
                }

                _events.Emit(new MatchEndedEvent(
                    LastOutcome.Result, LastOutcome.WinnerId, LastOutcome.TurnNumber, LastOutcome.Reason));
            }

            Record(command, CommandResult.Valid(), finished);
        }

        private ICommandSettler? FindSettler(IGameCommand command)
        {
            for (int i = 0; i < _settlers.Count; i++)
            {
                if (_settlers[i].CanSettle(command))
                {
                    return _settlers[i];
                }
            }

            return null;
        }

        private void Record(IGameCommand command, CommandResult result, bool? forceFinished = null)
        {
            bool accepted = result.IsValid;
            bool finished = forceFinished ?? _state.IsFinished;
            _history.Add(new MatchStepRecord(
                _history.Count,
                command.GetType().Name,
                command.PlayerId,
                accepted,
                accepted ? CommandError.None : result.Error,
                _phases.CurrentPhase,
                finished));
        }
    }
}
