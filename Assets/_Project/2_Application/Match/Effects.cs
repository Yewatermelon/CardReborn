using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Effects
{
    /// <summary>
    /// 效果执行上下文：提供结算所需的状态、配置、事件接收器、源卡与出牌命令目标。
    /// 不可变（持有的状态可变，但上下文本身不被替换）。
    /// </summary>
    public sealed class EffectContext
    {
        public EffectContext(
            MatchState state,
            CardDatabase database,
            EventLog events,
            CardInstance sourceCard,
            int selfSeat,
            TargetRef commandTarget,
            SettlementContext? settlement = null)
        {
            State = Guard.NotNull(state, nameof(state));
            Database = Guard.NotNull(database, nameof(database));
            Events = Guard.NotNull(events, nameof(events));
            SourceCard = Guard.NotNull(sourceCard, nameof(sourceCard));
            SelfSeat = selfSeat;
            CommandTarget = commandTarget;
            Settlement = settlement;
        }

        public MatchState State { get; }

        public CardDatabase Database { get; }

        public EventLog Events { get; }

        /// <summary>打出/触发本效果的源卡。</summary>
        public CardInstance SourceCard { get; }

        /// <summary>效果拥有者座位。</summary>
        public int SelfSeat { get; }

        /// <summary>出牌命令指定的目标（战吼目标来源）。</summary>
        public TargetRef CommandTarget { get; }

        /// <summary>
        /// 结算上下文（可选）：用于效果执行期间回调触发分发器（如召唤后触发 OnSummon）。
        /// 直接调用 EffectExecutor 的测试可为 null。
        /// </summary>
        public SettlementContext? Settlement { get; }

        /// <summary>按 InstanceId 在双方战场查找随从；找不到返回 null。</summary>
        public CardInstance? FindMinion(int instanceId)
        {
            for (int p = 0; p < 2; p++)
            {
                Zone board = State.GetPlayer(p).Board;
                for (int i = 0; i < board.Count; i++)
                {
                    if (board.Cards[i].InstanceId == instanceId)
                    {
                        return board.Cards[i];
                    }
                }
            }

            return null;
        }
    }

    /// <summary>效果执行器契约：把 <see cref="IEffectData"/> 结算到状态并产出事件。</summary>
    public interface IEffectExecutor
    {
        void Execute(IEffectData effect, EffectContext context);
    }

    /// <summary>
    /// 效果分发器：按 <see cref="IEffectData"/> 运行时类型路由到对应执行器。
    /// 未知类型抛 <see cref="InvalidOperationException"/>（结算期属装配错误）。
    /// </summary>
    public sealed class EffectExecutor
    {
        private readonly Dictionary<Type, IEffectExecutor> _executors = new Dictionary<Type, IEffectExecutor>();

        public EffectExecutor()
        {
            Register<DamageEffectData>(new DamageExecutor());
            Register<HealEffectData>(new HealExecutor());
            Register<DrawCardEffectData>(new DrawCardExecutor());
            Register<SummonEffectData>(new SummonExecutor());
            Register<BuffEffectData>(new BuffExecutor());
            Register<GainManaEffectData>(new GainManaExecutor());
            Register<GainArmorEffectData>(new GainArmorExecutor());
            Register<DestroyEffectData>(new DestroyExecutor());
            CompositeExecutor composite = new CompositeExecutor();
            Register<CompositeEffectData>(composite);
            composite.SetDispatcher(this);
        }

        public void Register<T>(IEffectExecutor executor) where T : IEffectData
        {
            _executors[typeof(T)] = Guard.NotNull(executor, nameof(executor));
        }

        public void Execute(IEffectData effect, EffectContext context)
        {
            Guard.NotNull(effect, nameof(effect));
            if (!_executors.TryGetValue(effect.GetType(), out IEffectExecutor? executor))
            {
                throw new InvalidOperationException("未注册的效果类型：" + effect.GetType().Name + "。");
            }

            executor.Execute(effect, context);
        }
    }
}
