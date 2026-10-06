using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>RuleEngine partial：英雄技能命令校验。</summary>
    public static partial class RuleEngine
    {
        private static CommandResult ValidateHeroPower(
            MatchState state,
            PlayerState player,
            UseHeroPowerCommand cmd,
            CardDatabase database)
        {
            HeroPowerDefinition power = database.RequireHeroPower(player.Hero.HeroPowerKey);

            if (player.Hero.PowerUsedThisTurn)
            {
                return CommandResult.Invalid(CommandError.HeroPowerAlreadyUsed, "英雄技能本回合已使用。");
            }

            if (!player.Mana.CanSpend(power.Cost))
            {
                return CommandResult.Invalid(CommandError.NotEnoughMana,
                    "法力不足（技能需要 " + power.Cost + "，当前 " + player.Mana.Current + "）。");
            }

            return ValidateTarget(cmd.Target, power.TargetRule, state, player.Id);
        }
    }
}
