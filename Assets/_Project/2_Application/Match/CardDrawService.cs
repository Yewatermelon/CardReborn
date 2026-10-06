using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 抽牌系统结算（M3-T8；Docs/01 FR-5.9、§3.7）：牌库有牌入手（手牌满爆牌），空库触发递增疲劳伤害。
    /// 牌库顶约定同 MatchFactory：<c>Deck.Cards[Count-1]</c>。无静态可变状态；由 M4 状态机/抽牌效果调用。
    /// </summary>
    public static class CardDrawService
    {
        /// <summary>逐张抽 <paramref name="count"/> 张并返回结算数据；不发事件、不判断对局是否结束。</summary>
        public static DrawOutcome Draw(PlayerState player, int count)
        {
            Guard.NotNull(player, nameof(player));
            Guard.Positive(count, nameof(count));

            List<int> drawn = new List<int>();
            List<int> burned = new List<int>();
            int fatigueDamage = 0;

            for (int i = 0; i < count; i++)
            {
                if (player.Deck.Count > 0)
                {
                    CardInstance top = player.Deck.Cards[player.Deck.Count - 1];
                    if (player.Hand.CanAdd())
                    {
                        player.Hand.MoveIn(top, player.Deck);
                        drawn.Add(top.InstanceId);
                    }
                    else
                    {
                        // 爆牌：移出牌库、入坟场，不入手牌（不触发亡语，M4 效果系统再细化）。
                        player.Deck.Remove(top);
                        player.Graveyard.Add(top);
                        burned.Add(top.InstanceId);
                    }
                }
                else
                {
                    player.FatigueCounter += 1;
                    player.Hero.Health -= player.FatigueCounter;
                    fatigueDamage += player.FatigueCounter;
                }
            }

            return new DrawOutcome(
                drawn,
                burned,
                fatigueDamage,
                player.FatigueCounter,
                player.Hero.Health <= 0);
        }
    }
}
