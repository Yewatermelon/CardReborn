using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// 玩家座位状态：固定 <see cref="Id"/>（0/1），聚合英雄与法力。
    /// 卡牌分区（牌库/手牌/战场/坟场）在 M3-T2 接入。
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerState(int id, HeroState hero, ManaPool mana)
        {
            Id = id;
            Hero = Guard.NotNull(hero, nameof(hero));
            Mana = Guard.NotNull(mana, nameof(mana));
        }

        /// <summary>座位 Id（0 = 先手候选位 / 1，业务标识，不是集合下标）。</summary>
        public int Id { get; }

        public HeroState Hero { get; }

        public ManaPool Mana { get; }
    }
}
