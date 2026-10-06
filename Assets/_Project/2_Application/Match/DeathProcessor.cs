using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 死亡管线（M4-T7；Docs/01 §3.4.7、FR-5.7）：每次命令结算后收集双方场上
    /// Health ≤ 0 的随从，按"移除战场 → 触发 OnDeath 亡语 → 移入坟场 → emit CardDeathEvent"
    /// 顺序处理。亡语可能产生新死亡，故外层循环至无新死亡。
    /// 英雄死亡不在此处理（英雄不在 Board），由 MatchEvaluator 判定终局。
    /// </summary>
    public sealed class DeathProcessor
    {
        /// <summary>处理双方场上所有 Health≤0 的随从，循环至无新死亡。</summary>
        public void Process(SettlementContext ctx)
        {
            Guard.NotNull(ctx, nameof(ctx));

            bool anyDied;
            do
            {
                anyDied = false;
                foreach (PlayerState player in ctx.State.Players)
                {
                    anyDied |= ProcessPlayer(ctx, player);
                }
            }
            while (anyDied);
        }

        private static bool ProcessPlayer(SettlementContext ctx, PlayerState player)
        {
            List<CardInstance> dead = CollectDead(player.Board);
            if (dead.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < dead.Count; i++)
            {
                CardInstance card = dead[i];
                player.Board.Remove(card);
                ctx.Dispatcher.RaiseOnDeath(ctx, card);
                player.Graveyard.Add(card);
                ctx.Events.Emit(new CardDeathEvent(card.InstanceId));
            }

            return true;
        }

        private static List<CardInstance> CollectDead(Zone board)
        {
            List<CardInstance> dead = new List<CardInstance>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board.Cards[i].Health <= 0)
                {
                    dead.Add(board.Cards[i]);
                }
            }

            return dead;
        }
    }
}
