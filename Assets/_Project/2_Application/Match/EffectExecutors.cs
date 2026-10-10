using System;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Effects
{
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

    /// <summary>召唤随从到己方战场；满场软失败——跳过召唤不抛异常（NFR-7 满场不得崩溃）。</summary>
    internal sealed class SummonExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            SummonEffectData s = (SummonEffectData)effect;
            PlayerState self = context.State.GetPlayer(context.SelfSeat);
            CardDefinition def = context.Database.RequireCard(s.CardKey);

            for (int i = 0; i < s.Count; i++)
            {
                if (!self.Board.CanAdd())
                {
                    // 满场时不能召唤（Docs/01 规则表）：效果软失败，不中断结算链。
                    break;
                }

                CardInstance minion = CardInstance.FromDefinition(
                    def, context.State.AllocateInstanceId(), context.SelfSeat);
                self.Board.Add(minion);

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

    /// <summary>获得临时法力：增加效果拥有者的当前法力值。</summary>
    internal sealed class GainManaExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            GainManaEffectData d = (GainManaEffectData)effect;
            PlayerState player = context.State.GetPlayer(context.SelfSeat);
            player.Mana.Gain(d.Amount);
        }
    }

    /// <summary>获得护甲：为效果拥有者英雄增加护甲。</summary>
    internal sealed class GainArmorExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            GainArmorEffectData d = (GainArmorEffectData)effect;
            HeroState hero = context.State.GetPlayer(context.SelfSeat).Hero;
            hero.GainArmor(d.Amount);
        }
    }

    /// <summary>
    /// 摧毁目标随从：移出战场 → 触发 OnDeath → 移入坟场 → emit CardDeathEvent。
    /// 复用 DeathProcessor 的处理顺序（Docs/01 §3.4.7）。
    /// </summary>
    internal sealed class DestroyExecutor : IEffectExecutor
    {
        public void Execute(IEffectData effect, EffectContext context)
        {
            TargetRef target = context.CommandTarget;
            if (target.Kind != TargetKind.Minion)
            {
                throw new InvalidOperationException("DestroyEffect 需要随从目标。");
            }

            CardInstance? minion = context.FindMinion(target.TargetId);
            if (minion == null)
            {
                throw new InvalidOperationException("摧毁目标随从不存在：" + target.TargetId);
            }

            for (int p = 0; p < 2; p++)
            {
                PlayerState owner = context.State.GetPlayer(p);
                if (!owner.Board.Contains(minion))
                {
                    continue;
                }

                owner.Board.Remove(minion);
                context.Settlement?.Dispatcher.RaiseOnDeath(context.Settlement!, minion);
                owner.Graveyard.Add(minion);
                context.Events.Emit(new CardDeathEvent(minion.InstanceId));
                return;
            }
        }
    }

    /// <summary>
    /// 复合效果容器：顺序执行所有子效果。通过 SetDispatcher 注入外层分发器以避免循环依赖。
    /// </summary>
    internal sealed class CompositeExecutor : IEffectExecutor
    {
        private EffectExecutor? _dispatcher;

        internal void SetDispatcher(EffectExecutor dispatcher)
        {
            _dispatcher = Guard.NotNull(dispatcher, nameof(dispatcher));
        }

        public void Execute(IEffectData effect, EffectContext context)
        {
            if (_dispatcher == null)
            {
                throw new InvalidOperationException("CompositeExecutor 未初始化分发器。");
            }

            CompositeEffectData d = (CompositeEffectData)effect;
            for (int i = 0; i < d.Effects.Count; i++)
            {
                _dispatcher.Execute(d.Effects[i], context);
            }
        }
    }
}
