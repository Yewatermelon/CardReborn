using System;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 攻击结算器（M4-T6；Docs/01 §3.4）：处理 <see cref="AttackCommand"/>，
    /// 攻击者对目标造成攻击力伤害；随从交换时双方同时互伤；圣盾抵消一次；剧毒必杀。
    /// 死亡处理（移除/亡语）留 M4-T7；此处只扣血并消耗攻击次数。
    /// </summary>
    public sealed class AttackSettler : ICommandSettler
    {
        public bool CanSettle(IGameCommand command) => command is AttackCommand;

        public void Settle(SettlementContext context, IGameCommand command)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(command, nameof(command));

            AttackCommand cmd = (AttackCommand)command;
            PlayerState self = context.State.GetPlayer(cmd.PlayerId);
            int enemyId = cmd.PlayerId == 0 ? 1 : 0;
            PlayerState enemy = context.State.GetPlayer(enemyId);

            CardInstance? attacker = FindById(self.Board, cmd.AttackerInstanceId);
            if (attacker == null)
            {
                throw new InvalidOperationException("攻击结算找不到攻击者：" + cmd.AttackerInstanceId);
            }

            if (cmd.Target.Kind == TargetKind.Hero)
            {
                int amount = enemy.Hero.TakeDamage(attacker.Attack);
                context.Events.Emit(new DamageEvent(
                    attacker.InstanceId, null, enemyId, amount, divineShieldConsumed: false));
            }
            else
            {
                CardInstance? defender = FindById(enemy.Board, cmd.Target.TargetId);
                if (defender == null)
                {
                    throw new InvalidOperationException("攻击结算找不到防御者：" + cmd.Target.TargetId);
                }

                bool attackerPoison = attacker.Keywords.Has(Keyword.Poisonous);
                bool defenderPoison = defender.Keywords.Has(Keyword.Poisonous);

                // 同时结算：双方互相造成对方攻击力的伤害（状态同时写入）。
                int dmgToDefender = defender.TakeDamage(attacker.Attack, attackerPoison);
                int dmgToAttacker = attacker.TakeDamage(defender.Attack, defenderPoison);

                // 圣盾消耗判定：攻击方攻击力 > 0 但实际扣血为 0 ⇔ 圣盾抵消。
                bool defenderShield = attacker.Attack > 0 && dmgToDefender == 0;
                bool attackerShield = defender.Attack > 0 && dmgToAttacker == 0;

                context.Events.Emit(new DamageEvent(
                    attacker.InstanceId, defender.InstanceId, null, dmgToDefender, defenderShield));
                context.Events.Emit(new DamageEvent(
                    defender.InstanceId, attacker.InstanceId, null, dmgToAttacker, attackerShield));
            }

            attacker.AttacksUsedThisTurn++;
            context.Events.Emit(new AttackDeclaredEvent(
                attacker.InstanceId,
                cmd.Target.Kind == TargetKind.Minion ? cmd.Target.TargetId : (int?)null,
                cmd.Target.Kind == TargetKind.Hero ? cmd.Target.TargetId : (int?)null));
        }

        private static CardInstance? FindById(Zone zone, int instanceId)
        {
            for (int i = 0; i < zone.Count; i++)
            {
                if (zone.Cards[i].InstanceId == instanceId)
                {
                    return zone.Cards[i];
                }
            }

            return null;
        }
    }
}
