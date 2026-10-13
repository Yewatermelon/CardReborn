using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Network
{
    /// <summary>
    /// Soak 决策策略（M14-T7）：脚本化、简单可复现，只发合法命令（AC-3 被拒数理想为 0）。
    /// 优先级：可出随从（费用 ≤ 法力、无目标）→ 可攻击随从（敌方存活嘲讽优先，
    /// 否则敌方英雄；Rush 不能打英雄）→ 结束回合（恒合法）。
    /// 决策数据来自座位自己的同步视图（与真实客户端同口径），不读权威内部状态。
    /// </summary>
    public static class SoakPolicy
    {
        /// <summary>为 seat 决策下一条命令；无牌可出且无兵可攻时结束回合。</summary>
        public static IGameCommand NextCommand(MatchState view, CardDatabase database, int seat)
        {
            PlayerState player = view.GetPlayer(seat);
            PlayerState opponent = view.GetPlayer(1 - seat);

            IGameCommand? play = TryPlayMinion(database, player);
            if (play is not null)
            {
                return play;
            }

            IGameCommand? attack = TryAttack(player, opponent);
            if (attack is not null)
            {
                return attack;
            }

            return new EndTurnCommand(seat);
        }

        /// <summary>出第一张可出的无目标随从（费用足够且战场未满）。</summary>
        private static IGameCommand? TryPlayMinion(CardDatabase database, PlayerState player)
        {
            if (!player.Board.CanAdd())
            {
                return null;
            }

            Zone hand = player.Hand;
            for (int i = 0; i < hand.Cards.Count; i++)
            {
                CardInstance card = hand.Cards[i];
                CardDefinition definition = database.RequireCard(card.CardKey);
                if (definition.Type == CardType.Minion
                    && definition.TargetRule == TargetRule.None
                    && player.Mana.CanSpend(definition.Cost))
                {
                    return new PlayCardCommand(player.Id, card.InstanceId, TargetRef.None);
                }
            }

            return null;
        }

        /// <summary>用第一个可攻击随从发起攻击：嘲讽优先；Rush 在无嘲讽时不攻击。</summary>
        private static IGameCommand? TryAttack(PlayerState player, PlayerState opponent)
        {
            CardInstance? taunt = FindTaunt(opponent);
            Zone board = player.Board;
            for (int i = 0; i < board.Cards.Count; i++)
            {
                CardInstance minion = board.Cards[i];
                if (!CanAttack(minion))
                {
                    continue;
                }

                if (taunt is not null)
                {
                    return new AttackCommand(player.Id, minion.InstanceId, TargetRef.ForMinion(taunt.InstanceId));
                }

                bool rushOnly = minion.Keywords.Has(Keyword.Rush) && !minion.Keywords.Has(Keyword.Charge);
                if (!rushOnly)
                {
                    return new AttackCommand(player.Id, minion.InstanceId, TargetRef.ForHero(opponent.Id));
                }
            }

            return null;
        }

        /// <summary>随从本回合是否可攻击：活着、有攻击力、次数未用尽、无失调/冻结。
        /// 注意 StatusSet.Has 是"包含全部位"语义——失调与冻结必须分别检查。</summary>
        private static bool CanAttack(CardInstance minion)
        {
            if (minion.Attack <= 0 || minion.Health <= 0)
            {
                return false;
            }

            int maxAttacks = minion.Keywords.Has(Keyword.Windfury) ? 2 : 1;
            if (minion.AttacksUsedThisTurn >= maxAttacks)
            {
                return false;
            }

            bool haste = minion.Keywords.Has(Keyword.Charge) || minion.Keywords.Has(Keyword.Rush);
            bool blocked = minion.Statuses.Has(StatusFlags.SummoningSickness)
                || minion.Statuses.Has(StatusFlags.Frozen);
            return haste || !blocked;
        }

        /// <summary>找敌方第一个存活嘲讽随从（嘲讽在场时必须优先攻击嘲讽）。</summary>
        private static CardInstance? FindTaunt(PlayerState opponent)
        {
            Zone board = opponent.Board;
            for (int i = 0; i < board.Cards.Count; i++)
            {
                CardInstance minion = board.Cards[i];
                if (minion.Health > 0 && minion.Keywords.Has(Keyword.Taunt))
                {
                    return minion;
                }
            }

            return null;
        }
    }
}
