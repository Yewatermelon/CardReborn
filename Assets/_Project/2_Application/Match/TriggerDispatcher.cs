using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Effects
{
    /// <summary>
    /// 触发分发器（M4-T5；Docs/01 §4.3）：统一按 <see cref="Trigger"/> 时机执行卡牌效果。
    /// 持有 <see cref="EffectExecutor"/> 与递归深度计数器（防死循环）。
    /// 每局一个实例，由 MatchController 经 SettlementContext 传入结算器。
    /// </summary>
    public sealed class TriggerDispatcher
    {
        /// <summary>递归触发深度上限，超限抛 <see cref="InvalidOperationException"/>。</summary>
        public const int MaxDepth = 64;

        private readonly EffectExecutor _executor = new EffectExecutor();
        private int _depth;

        /// <summary>出牌战吼：执行该卡的 OnPlay 效果。</summary>
        public void RaiseOnPlay(SettlementContext ctx, CardInstance card, TargetRef target)
        {
            Guard.NotNull(ctx, nameof(ctx));
            Guard.NotNull(card, nameof(card));
            RaiseCardEffects(ctx, card, Trigger.OnPlay, target);
        }

        /// <summary>亡语：执行该卡的 OnDeath 效果（源卡 = 死亡随从）。</summary>
        public void RaiseOnDeath(SettlementContext ctx, CardInstance deadCard)
        {
            Guard.NotNull(ctx, nameof(ctx));
            Guard.NotNull(deadCard, nameof(deadCard));
            RaiseCardEffects(ctx, deadCard, Trigger.OnDeath, TargetRef.None);
        }

        /// <summary>召唤进场：执行该卡的 OnSummon 效果。</summary>
        public void RaiseOnSummon(SettlementContext ctx, CardInstance summoned)
        {
            Guard.NotNull(ctx, nameof(ctx));
            Guard.NotNull(summoned, nameof(summoned));
            RaiseCardEffects(ctx, summoned, Trigger.OnSummon, TargetRef.None);
        }

        /// <summary>回合开始：遍历 seat 方场上随从，执行各卡的 OnTurnStart 效果。</summary>
        public void RaiseOnTurnStart(SettlementContext ctx, int seat)
        {
            Guard.NotNull(ctx, nameof(ctx));
            RaiseBoardEffects(ctx, seat, Trigger.OnTurnStart);
        }

        /// <summary>回合结束：遍历 seat 方场上随从，执行各卡的 OnTurnEnd 效果。</summary>
        public void RaiseOnTurnEnd(SettlementContext ctx, int seat)
        {
            Guard.NotNull(ctx, nameof(ctx));
            RaiseBoardEffects(ctx, seat, Trigger.OnTurnEnd);
        }

        private void RaiseCardEffects(
            SettlementContext ctx, CardInstance source, Trigger trigger, TargetRef target)
        {
            CardDefinition def = ctx.Database.RequireCard(source.CardKey);
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(def.Effects);

            _depth++;
            try
            {
                if (_depth > MaxDepth)
                {
                    throw new InvalidOperationException(
                        "触发链深度超过上限 " + MaxDepth + "（疑似死循环）。");
                }

                EffectContext effectCtx = new EffectContext(
                    ctx.State, ctx.Database, ctx.Events, source, source.OwnerId, target, ctx);
                for (int i = 0; i < effects.Count; i++)
                {
                    if (effects[i].Trigger == trigger)
                    {
                        _executor.Execute(effects[i].Effect, effectCtx);
                    }
                }
            }
            finally
            {
                _depth--;
            }
        }

        private void RaiseBoardEffects(SettlementContext ctx, int seat, Trigger trigger)
        {
            Zone board = ctx.State.GetPlayer(seat).Board;
            // 快照：触发效果可能修改战场（如召唤），遍历快照避免迭代器失效
            List<CardInstance> snapshot = new List<CardInstance>(board.Count);
            for (int i = 0; i < board.Count; i++)
            {
                snapshot.Add(board.Cards[i]);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                RaiseCardEffects(ctx, snapshot[i], trigger, TargetRef.None);
            }
        }
    }
}
