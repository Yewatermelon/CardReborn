using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>RuleEngine partial：目标存在性、归属与合法性校验。</summary>
    public static partial class RuleEngine
    {
        private static CommandResult ValidateTarget(
            TargetRef target,
            TargetRule rule,
            MatchState state,
            int playerId)
        {
            if (rule == TargetRule.None)
            {
                if (!target.IsNone)
                {
                    return CommandResult.Invalid(CommandError.InvalidTarget, "该指令不需要目标。");
                }

                return CommandResult.Valid();
            }

            if (target.IsNone)
            {
                return CommandResult.Invalid(CommandError.TargetRequired, "该指令需要目标。");
            }

            if (!TargetExists(state, target))
            {
                return CommandResult.Invalid(CommandError.InvalidTarget, "目标不存在。");
            }

            switch (rule)
            {
                case TargetRule.Any:
                    return CommandResult.Valid();
                case TargetRule.Enemy:
                    return IsEnemyTarget(state, playerId, target)
                        ? CommandResult.Valid()
                        : CommandResult.Invalid(CommandError.InvalidTarget, "目标必须是敌方角色。");
                case TargetRule.Friendly:
                    return IsFriendlyTarget(state, playerId, target)
                        ? CommandResult.Valid()
                        : CommandResult.Invalid(CommandError.InvalidTarget, "目标必须是友方角色。");
                case TargetRule.EnemyMinion:
                    return IsEnemyMinion(state, playerId, target)
                        ? CommandResult.Valid()
                        : CommandResult.Invalid(CommandError.InvalidTarget, "目标必须是敌方随从。");
                case TargetRule.FriendlyMinion:
                    return IsFriendlyMinion(state, playerId, target)
                        ? CommandResult.Valid()
                        : CommandResult.Invalid(CommandError.InvalidTarget, "目标必须是友方随从。");
                case TargetRule.AnyMinion:
                    return target.Kind == TargetKind.Minion && IsMinionAlive(state, target)
                        ? CommandResult.Valid()
                        : CommandResult.Invalid(CommandError.InvalidTarget, "目标必须是场上随从。");
                default:
                    return CommandResult.Invalid(CommandError.InvalidTarget, "未知的目标规则。");
            }
        }

        private static bool TargetExists(MatchState state, TargetRef target)
        {
            if (target.Kind == TargetKind.None)
            {
                return false;
            }

            if (target.Kind == TargetKind.Hero)
            {
                return IsKnownPlayerId(state, target.TargetId);
            }

            if (target.Kind == TargetKind.Minion)
            {
                return IsMinionAlive(state, target);
            }

            return false;
        }

        private static bool IsKnownPlayerId(MatchState state, int playerId)
        {
            foreach (PlayerState p in state.Players)
            {
                if (p.Id == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMinionAlive(MatchState state, TargetRef target)
        {
            if (target.Kind != TargetKind.Minion)
            {
                return false;
            }

            foreach (PlayerState p in state.Players)
            {
                for (int i = 0; i < p.Board.Count; i++)
                {
                    CardInstance card = p.Board.Cards[i];
                    if (card.InstanceId == target.TargetId && card.Health > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsEnemyTarget(MatchState state, int playerId, TargetRef target)
        {
            if (target.Kind == TargetKind.Hero)
            {
                return target.TargetId != playerId && IsKnownPlayerId(state, target.TargetId);
            }

            if (target.Kind == TargetKind.Minion)
            {
                foreach (PlayerState p in state.Players)
                {
                    if (p.Id == playerId)
                    {
                        continue;
                    }

                    for (int i = 0; i < p.Board.Count; i++)
                    {
                        if (p.Board.Cards[i].InstanceId == target.TargetId)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsFriendlyTarget(MatchState state, int playerId, TargetRef target)
        {
            if (target.Kind == TargetKind.Hero)
            {
                return target.TargetId == playerId;
            }

            if (target.Kind == TargetKind.Minion)
            {
                PlayerState? player = FindPlayer(state, playerId);
                if (player == null)
                {
                    return false;
                }

                for (int i = 0; i < player.Board.Count; i++)
                {
                    if (player.Board.Cards[i].InstanceId == target.TargetId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static PlayerState? FindPlayer(MatchState state, int playerId)
        {
            foreach (PlayerState p in state.Players)
            {
                if (p.Id == playerId)
                {
                    return p;
                }
            }

            return null;
        }

        private static bool IsEnemyMinion(MatchState state, int playerId, TargetRef target)
        {
            return target.Kind == TargetKind.Minion
                && IsEnemyTarget(state, playerId, target)
                && IsMinionAlive(state, target);
        }

        private static bool IsFriendlyMinion(MatchState state, int playerId, TargetRef target)
        {
            return target.Kind == TargetKind.Minion
                && IsFriendlyTarget(state, playerId, target)
                && IsMinionAlive(state, target);
        }
    }
}
