using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>RuleEngine partial：攻击命令校验。</summary>
    public static partial class RuleEngine
    {
        private static CommandResult ValidateAttack(
            MatchState state,
            PlayerState attackerPlayer,
            AttackCommand cmd,
            CardDatabase database)
        {
            CardInstance? attacker = FindById(attackerPlayer.Board, cmd.AttackerInstanceId);
            if (attacker == null || attacker.OwnerId != attackerPlayer.Id)
            {
                return CommandResult.Invalid(CommandError.AttackerNotOnBoard, "攻击者不在我方场上。");
            }

            if (attacker.Statuses.Has(StatusFlags.SummoningSickness)
                && !IgnoresSummoningSickness(attacker))
            {
                return CommandResult.Invalid(CommandError.SummoningSickness, "攻击者处于召唤失调状态。");
            }

            int maxAttacks = attacker.Keywords.Has(Keyword.Windfury) ? 2 : 1;
            if (attacker.AttacksUsedThisTurn >= maxAttacks)
            {
                return CommandResult.Invalid(CommandError.AlreadyAttacked, "攻击者本回合攻击次数已用完。");
            }

            if (cmd.Target.IsNone)
            {
                return CommandResult.Invalid(CommandError.InvalidTarget, "攻击需要指定目标。");
            }

            if (!TargetExists(state, cmd.Target))
            {
                return CommandResult.Invalid(CommandError.InvalidTarget, "目标不存在。");
            }

            if (!IsEnemyTarget(state, attackerPlayer.Id, cmd.Target))
            {
                return CommandResult.Invalid(CommandError.InvalidTarget, "只能攻击敌方目标。");
            }

            if (EnemyHasTaunt(state, attackerPlayer.Id)
                && !IsEnemyTauntMinion(state, cmd.Target, attackerPlayer.Id))
            {
                return CommandResult.Invalid(CommandError.MustTargetTaunt,
                    "敌方存在嘲讽随从，必须优先攻击嘲讽目标。");
            }

            return CommandResult.Valid();
        }

        private static bool IgnoresSummoningSickness(CardInstance card)
        {
            return card.Keywords.Has(Keyword.Charge) || card.Keywords.Has(Keyword.Rush);
        }

        private static bool EnemyHasTaunt(MatchState state, int playerId)
        {
            foreach (PlayerState p in state.Players)
            {
                if (p.Id == playerId)
                {
                    continue;
                }

                for (int i = 0; i < p.Board.Count; i++)
                {
                    CardInstance card = p.Board.Cards[i];
                    if (card.Keywords.Has(Keyword.Taunt) && card.Health > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsEnemyTauntMinion(MatchState state, TargetRef target, int playerId)
        {
            if (target.Kind != TargetKind.Minion)
            {
                return false;
            }

            foreach (PlayerState p in state.Players)
            {
                if (p.Id == playerId)
                {
                    continue;
                }

                for (int i = 0; i < p.Board.Count; i++)
                {
                    CardInstance card = p.Board.Cards[i];
                    if (card.InstanceId == target.TargetId
                        && card.Keywords.Has(Keyword.Taunt)
                        && card.Health > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
