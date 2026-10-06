using System;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 结束回合结算器（M4-T2；Docs/01 §3.2）：通过校验的 <see cref="EndTurnCommand"/>
    /// 按 <c>Main→TurnEnd→TurnStart(簿记)→Draw(抽1)→Main</c> 推进，同步切换行动方。
    /// 先手第 1 回合不抽由 MatchFactory 直接产出 Main/Turn1 保证；后续每回合 Draw 抽 1。
    /// 不含 OnTurnStart/OnTurnEnd 触发（M4-T5）；死亡/胜负面在结算后由控制器统一判定。
    /// </summary>
    public sealed class EndTurnSettler : ICommandSettler
    {
        /// <summary>每回合抽牌数（Docs/01 §3.2 Draw 阶段固定 1 张）。</summary>
        public const int CardsDrawnPerTurn = 1;

        public bool CanSettle(IGameCommand command)
        {
            return command is EndTurnCommand;
        }

        public void Settle(SettlementContext context, IGameCommand command)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(command, nameof(command));

            RequireMove(context.Phases, TurnPhase.TurnEnd);

            int nextSeat = context.State.ActivePlayerId == 0 ? 1 : 0;
            context.State.ActivePlayerId = nextSeat;
            context.State.TurnNumber += 1;

            RequireMove(context.Phases, TurnPhase.TurnStart);
            BeginTurnFor(context.State.GetPlayer(nextSeat), context.Database.Rules);

            RequireMove(context.Phases, TurnPhase.Draw);
            CardDrawService.Draw(context.State.GetPlayer(nextSeat), CardsDrawnPerTurn);

            RequireMove(context.Phases, TurnPhase.Main);
        }

        /// <summary>
        /// 回合开始簿记（§3.2 TurnStart）：水晶增长并回满（规则本体在 ManaPool，M3-T1）、
        /// 英雄技能次数清零、己方随从攻击次数清零并解除召唤失调（下回合可攻击）。
        /// </summary>
        private static void BeginTurnFor(PlayerState player, RulesConfig rules)
        {
            Guard.NotNull(player, nameof(player));
            Guard.NotNull(rules, nameof(rules));

            player.Mana.BeginTurn(rules.ManaLimit);
            player.Hero.PowerUsedThisTurn = false;

            for (int i = 0; i < player.Board.Count; i++)
            {
                CardInstance minion = player.Board.Cards[i];
                minion.AttacksUsedThisTurn = 0;
                minion.Statuses.Remove(StatusFlags.SummoningSickness);
            }
        }

        private static void RequireMove(TurnStateMachine phases, TurnPhase target)
        {
            Result result = phases.MoveTo(target);
            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    "回合阶段流转失败：" + target + "（" + result.ErrorCode + "）");
            }
        }
    }
}
