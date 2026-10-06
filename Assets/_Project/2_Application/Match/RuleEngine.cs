using System;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 对局规则权威校验器（M3-T6）：只读查询、不修改状态；四种 <see cref="IGameCommand"/> 的合法性入口。
    /// 失败时返回携带 <see cref="CommandError"/> 与补充说明的 <see cref="CommandResult"/>。
    /// 校验顺序：结构（null/座位/回合/阶段）→ 命令专用检查 → 目标检查。
    /// </summary>
    public static partial class RuleEngine
    {
        public static CommandResult Validate(MatchState state, IGameCommand command, CardDatabase database)
        {
            if (command == null)
            {
                return CommandResult.Invalid(CommandError.UnknownCommand, "命令不能为 null。");
            }

            if (state.IsFinished)
            {
                return CommandResult.Invalid(CommandError.InvalidTarget, "对局已结束，无法执行任何命令。");
            }

            PlayerState player;
            try
            {
                player = state.GetPlayer(command.PlayerId);
            }
            catch (ArgumentException)
            {
                return CommandResult.Invalid(CommandError.NotYourTurn, "玩家座位不存在。");
            }

            if (player.Id != state.ActivePlayerId)
            {
                return CommandResult.Invalid(CommandError.NotYourTurn, "当前不是你的回合。");
            }

            if (state.Phase != TurnPhase.Main)
            {
                return CommandResult.Invalid(CommandError.NotYourTurn, "当前不是主阶段（Main）。");
            }

            switch (command)
            {
                case PlayCardCommand play:
                    return ValidatePlayCard(state, player, play, database);
                case AttackCommand attack:
                    return ValidateAttack(state, player, attack, database);
                case UseHeroPowerCommand power:
                    return ValidateHeroPower(state, player, power, database);
                case EndTurnCommand _:
                    return CommandResult.Valid();
                default:
                    return CommandResult.Invalid(CommandError.UnknownCommand, "不支持的命令类型。");
            }
        }

        // -----------------------------------------------------------------
        // 出牌
        // -----------------------------------------------------------------

        private static CommandResult ValidatePlayCard(
            MatchState state,
            PlayerState player,
            PlayCardCommand cmd,
            CardDatabase database)
        {
            CardInstance? card = FindById(player.Hand, cmd.CardInstanceId);
            if (card == null)
            {
                return CommandResult.Invalid(CommandError.CardNotInHand, "手牌中找不到该卡牌实例。");
            }

            CardDefinition definition = database.RequireCard(card.CardKey);

            if (!player.Mana.CanSpend(definition.Cost))
            {
                return CommandResult.Invalid(CommandError.NotEnoughMana,
                    "法力不足（需要 " + definition.Cost + "，当前 " + player.Mana.Current + "）。");
            }

            if (definition.Type == CardType.Minion && !player.Board.CanAdd())
            {
                return CommandResult.Invalid(CommandError.BoardFull, "战场已满。");
            }

            return ValidateTarget(cmd.Target, definition.TargetRule, state, player.Id);
        }

        // -----------------------------------------------------------------
        // 通用辅助
        // -----------------------------------------------------------------

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
