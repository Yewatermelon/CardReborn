using System;
using System.Collections.Generic;
using Card.Application.Match.Effects;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match
{
    /// <summary>
    /// 英雄技能结算器（M4-T8；Docs/01 §3.6、FR-5.10）：处理 <see cref="UseHeroPowerCommand"/>，
    /// 消耗法力 → 执行技能配置效果（直接走 T4 效果框架，非 Trigger 时机）→ 标记本回合已用。
    /// 校验由 RuleEngine 在入队前完成（法力/次数/目标）；结算期失败属装配错误直接抛。
    /// </summary>
    public sealed class HeroPowerSettler : ICommandSettler
    {
        private readonly EffectExecutor _effects = new EffectExecutor();

        public bool CanSettle(IGameCommand command) => command is UseHeroPowerCommand;

        public void Settle(SettlementContext context, IGameCommand command)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(command, nameof(command));

            UseHeroPowerCommand cmd = (UseHeroPowerCommand)command;
            PlayerState player = context.State.GetPlayer(cmd.PlayerId);
            HeroPowerDefinition power = context.Database.RequireHeroPower(player.Hero.HeroPowerKey);

            Result spend = player.Mana.Spend(power.Cost);
            if (spend.IsFailure)
            {
                throw new InvalidOperationException("英雄技能法力扣除失败：" + spend.ErrorCode);
            }

            player.Hero.PowerUsedThisTurn = true;

            // 技能效果是即时效果（非 Trigger 时机），直接执行不走 Dispatcher。
            // 用技能配置构造英雄伪卡作为 SourceCard（InstanceId=0 保留，区分于真实卡牌实例）。
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(power.Effects);
            CardDefinition pseudoDef = new CardDefinition
            {
                Id = 0,
                Key = power.Key,
                Cost = power.Cost,
                Type = CardType.Spell,
                Attack = 0,
                Health = 0,
                TargetRule = power.TargetRule,
                Effects = power.Effects
            };
            CardInstance heroPseudoCard = CardInstance.FromDefinition(pseudoDef, 0, cmd.PlayerId);
            EffectContext effectCtx = new EffectContext(
                context.State, context.Database, context.Events, heroPseudoCard, cmd.PlayerId, cmd.Target, context);

            for (int i = 0; i < effects.Count; i++)
            {
                _effects.Execute(effects[i].Effect, effectCtx);
            }
        }
    }
}
