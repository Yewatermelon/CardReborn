using System.Collections.Generic;
using System.Linq;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 只读对局视图 → 表现层视图同步器（M6-T1；铁律 4/12）：
    /// 输入只有 <see cref="IReadOnlyMatchState"/>，不持可写状态、不判规则。
    /// 己方手牌显示卡面，对方手牌只显示牌背数量；随从带嘲讽关键词时亮嘲讽标记
    /// （关键词是状态数据，等同渲染攻击力，不是规则判断）。
    /// </summary>
    public sealed class BattleViewSynchronizer
    {
        private static readonly CardViewData CardBack =
            new CardViewData(string.Empty, string.Empty, 0, 0, 0, string.Empty, CardType.Spell);

        private int _localSeat;
        private int _enemySeat;
        private readonly CardDatabase _database;
        private readonly BattleUi _ui;

        public BattleViewSynchronizer(
            BattleUi ui,
            CardDatabase database,
            int localSeat,
            int enemySeat)
        {
            _ui = ui;
            _database = database;
            _localSeat = localSeat;
            _enemySeat = enemySeat;
        }

        /// <summary>按只读对局快照全量刷新各区（事件泵派发后调用，非每帧）。</summary>
        public void Push(IReadOnlyMatchState state)
        {
            _ui.TurnLabel.text = "回合 " + state.TurnNumber.ToString();
            PushPlayer(state.GetPlayer(_localSeat), isLocal: true);
            PushPlayer(state.GetPlayer(_enemySeat), isLocal: false);
        }

        /// <summary>热座切换（M6-T3）：交换本地/对手座位，下次 Push 时手牌卡面/牌背互换。</summary>
        public void SwitchSeats()
        {
            (_localSeat, _enemySeat) = (_enemySeat, _localSeat);
        }

        private void PushPlayer(IReadOnlyPlayerState player, bool isLocal)
        {
            (HeroView hero, ManaView mana, HandView hand, BoardView board) = isLocal
                ? (_ui.LocalHero, _ui.LocalMana, _ui.LocalHand, _ui.LocalBoard)
                : (_ui.EnemyHero, _ui.EnemyMana, _ui.EnemyHand, _ui.EnemyBoard);

            hero.SetName(_database.RequireHero(player.Hero.HeroKey).NameKey);
            hero.SetData(player.Hero.Health, player.Hero.MaxHealth, player.Hero.Armor);
            mana.SetData(player.Mana.Current, player.Mana.Max);

            board.SetCards(player.Board.Cards
                .Select(c => CardViewData.FromInstance(_database.RequireCard(c.CardKey), c))
                .ToList());
            ApplyTauntMarks(board, player.Board.Cards);

            if (isLocal)
            {
                hand.SetCards(player.Hand.Cards
                    .Select(c => CardViewData.FromInstance(_database.RequireCard(c.CardKey), c))
                    .ToList());
            }
            else
            {
                hand.SetCards(Enumerable.Repeat((ICardViewData)CardBack, player.Hand.Count).ToList());
            }
        }

        private static void ApplyTauntMarks(BoardView board, IReadOnlyList<IReadOnlyCardInstance> cards)
        {
            for (int i = 0; i < board.ChildCount && i < cards.Count; i++)
            {
                bool taunt = (cards[i].KeywordFlags & Keyword.Taunt) != 0;
                board.Children[i].SetHighlight(taunt ? CardHighlight.Taunt : CardHighlight.None);
            }
        }
    }
}
