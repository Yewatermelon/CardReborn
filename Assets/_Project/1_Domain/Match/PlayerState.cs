using Card.Core;
using Card.Domain.Config;

namespace Card.Domain.Match
{
    /// <summary>
    /// 玩家座位状态：固定 <see cref="Id"/>（0/1），聚合英雄、法力与四个卡牌分区。
    /// 分区容量由 <see cref="RulesConfig"/> 提供（手牌/场上限；牌库/坟场不限）。
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerState(int id, HeroState hero, ManaPool mana, RulesConfig? rules = null)
        {
            Id = id;
            Hero = Guard.NotNull(hero, nameof(hero));
            Mana = Guard.NotNull(mana, nameof(mana));

            RulesConfig config = rules ?? new RulesConfig();
            Deck = new Zone(ZoneType.Deck, capacity: null);
            Hand = new Zone(ZoneType.Hand, config.HandLimit);
            Board = new Zone(ZoneType.Board, config.BoardLimit);
            Graveyard = new Zone(ZoneType.Graveyard, capacity: null);
        }

        /// <summary>座位 Id（0 = 先手候选位 / 1，业务标识，不是集合下标）。</summary>
        public int Id { get; }

        public HeroState Hero { get; }

        public ManaPool Mana { get; }

        public Zone Deck { get; }

        public Zone Hand { get; }

        public Zone Board { get; }

        public Zone Graveyard { get; }

        /// <summary>疲劳计数：牌库空时每次抽牌递增，M4 结算用（M3-T7 先提供字段）。</summary>
        public int FatigueCounter { get; set; }
    }
}
