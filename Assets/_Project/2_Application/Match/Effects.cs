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

    /// <summary>造成伤害：英雄走护甲先抵，随从直接扣血；圣盾留 T6 CombatResolver 处理。</summary>
    internal sealed class DamageExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            DamageEffectData d = (DamageEffectData)effect;
            TargetRef target = context.CommandTarget;

            if (target.Kind == TargetKind.Hero)
            {
                HeroState hero = context.State.GetPlayer(target.TargetId).Hero;
                hero.TakeDamage(d.Amount);
                context.Events.Emit(new DamageEvent(
                    sourceInstanceId: context.SourceCard.InstanceId,
                    targetInstanceId: null,
                    targetHeroSeat: target.TargetId,
                    amount: d.Amount,
                    divineShieldConsumed: false));
                return;
            }

            if (target.Kind == TargetKind.Minion)
            {
                CardInstance? minion = context.FindMinion(target.TargetId);
                if (minion == null)
                {
                    throw new InvalidOperationException("伤害目标随从不存在：" + target.TargetId);
                }

                minion.Health -= d.Amount;
                context.Events.Emit(new DamageEvent(
                    sourceInstanceId: context.SourceCard.InstanceId,
                    targetInstanceId: minion.InstanceId,
                    targetHeroSeat: null,
                    amount: d.Amount,
                    divineShieldConsumed: false));
                return;
            }

            // 无目标：对自己英雄生效（简化，用于自伤型法术）。
            HeroState self = context.State.GetPlayer(context.SelfSeat).Hero;
            self.TakeDamage(d.Amount);
            context.Events.Emit(new DamageEvent(
                sourceInstanceId: context.SourceCard.InstanceId,
                targetInstanceId: null,
                targetHeroSeat: context.SelfSeat,
                amount: d.Amount,
                divineShieldConsumed: false));
        }
    }

    /// <summary>治疗：英雄/随从均不超过上限。</summary>
    internal sealed class HealExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            HealEffectData h = (HealEffectData)effect;
            TargetRef target = context.CommandTarget;

            if (target.Kind == TargetKind.Hero)
            {
                HeroState hero = context.State.GetPlayer(target.TargetId).Hero;
                int healed = hero.Heal(h.Amount);
                context.Events.Emit(new HealingEvent(null, target.TargetId, healed));
                return;
            }

            if (target.Kind == TargetKind.Minion)
            {
                CardInstance? minion = context.FindMinion(target.TargetId);
                if (minion == null)
                {
                    throw new InvalidOperationException("治疗目标随从不存在：" + target.TargetId);
                }

                int before = minion.Health;
                minion.Health = Math.Min(minion.Health + h.Amount, minion.MaxHealth);
                context.Events.Emit(new HealingEvent(minion.InstanceId, null, minion.Health - before));
                return;
            }

            HeroState self = context.State.GetPlayer(context.SelfSeat).Hero;
            int healedSelf = self.Heal(h.Amount);
            context.Events.Emit(new HealingEvent(null, context.SelfSeat, healedSelf));
        }
    }

    /// <summary>抽牌：复用 CardDrawService，产出抽牌/爆牌/疲劳事件。</summary>
    internal sealed class DrawCardExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            DrawCardEffectData d = (DrawCardEffectData)effect;
            PlayerState player = context.State.GetPlayer(context.SelfSeat);
            DrawOutcome outcome = CardDrawService.Draw(player, d.Count);
            EmitDrawEvents(context.Events, context.SelfSeat, outcome);
        }

        private static void EmitDrawEvents(IEventSink events, int seat, DrawOutcome outcome)
        {
            for (int i = 0; i < outcome.DrawnInstanceIds.Count; i++)
            {
                events.Emit(new CardDrawnEvent(seat, outcome.DrawnInstanceIds[i]));
            }

            for (int i = 0; i < outcome.BurnedInstanceIds.Count; i++)
            {
                events.Emit(new CardBurnedEvent(seat, outcome.BurnedInstanceIds[i]));
            }

            if (outcome.FatigueDamage > 0)
            {
                events.Emit(new FatigueEvent(seat, outcome.FatigueDamage, outcome.FinalFatigueCounter));
            }
        }
    }

    /// <summary>召唤随从到己方战场（T4 不处理战场满，失败抛异常；T5 补）。</summary>
    internal sealed class SummonExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            SummonEffectData s = (SummonEffectData)effect;
            PlayerState self = context.State.GetPlayer(context.SelfSeat);
            CardDefinition def = context.Database.RequireCard(s.CardKey);

            for (int i = 0; i < s.Count; i++)
            {
                CardInstance minion = CardInstance.FromDefinition(
                    def, context.State.AllocateInstanceId(), context.SelfSeat);
                Result add = self.Board.Add(minion);
                if (add.IsFailure)
                {
                    throw new InvalidOperationException("召唤失败（战场已满）：" + add.ErrorCode);
                }

                // 召唤进场触发 OnSummon（链式触发，深度由 TriggerDispatcher 限制）。
                context.Settlement?.Dispatcher.RaiseOnSummon(context.Settlement, minion);
            }
        }
    }

    /// <summary>增益随从：攻击力/生命值按参数增减（T4 改基础值，临时增益留 T5）。</summary>
    internal sealed class BuffExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            BuffEffectData b = (BuffEffectData)effect;
            if (context.CommandTarget.Kind != TargetKind.Minion)
            {
                throw new InvalidOperationException("增益效果目标必须是随从。");
            }

            CardInstance? minion = context.FindMinion(context.CommandTarget.TargetId);
            if (minion == null)
            {
                throw new InvalidOperationException("增益目标随从不存在：" + context.CommandTarget.TargetId);
            }

            minion.Attack += b.Attack;
            minion.Health += b.Health;
        }
    }
}
