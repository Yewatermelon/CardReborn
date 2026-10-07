using System.Collections.Generic;
using Card.Domain.Config;

namespace Card.Domain.Match
{
    /// <summary>
    /// 卡牌实例的只读视图（M5-T8 ★）：只暴露读成员与值类型 flag 副本，
    /// 不暴露可变 <see cref="KeywordSet"/>/<see cref="StatusSet"/> 对象。
    /// 表现层与联网客户端只允许看到本接口（Docs/03 §5.6；ADR-14）。
    /// </summary>
    public interface IReadOnlyCardInstance
    {
        int InstanceId { get; }

        string CardKey { get; }

        int OwnerId { get; }

        int Attack { get; }

        int MaxHealth { get; }

        int Health { get; }

        ZoneType? CurrentZone { get; }

        /// <summary>关键词位副本（值类型，读取安全）。</summary>
        Keyword KeywordFlags { get; }

        /// <summary>状态位副本（值类型，读取安全）。</summary>
        StatusFlags StatusFlags { get; }
    }

    /// <summary>分区的只读视图（M5-T8）：卡牌列表协变为只读卡牌。</summary>
    public interface IReadOnlyZone
    {
        ZoneType Type { get; }

        int? Capacity { get; }

        int Count { get; }

        IReadOnlyList<IReadOnlyCardInstance> Cards { get; }
    }

    /// <summary>法力水晶的只读视图（M5-T8）。</summary>
    public interface IReadOnlyManaPool
    {
        int Max { get; }

        int Current { get; }

        bool CanSpend(int amount);
    }

    /// <summary>英雄运行时状态的只读视图（M5-T8）。</summary>
    public interface IReadOnlyHeroState
    {
        string HeroKey { get; }

        string HeroPowerKey { get; }

        int MaxHealth { get; }

        int Health { get; }

        int Armor { get; }

        bool PowerUsedThisTurn { get; }
    }

    /// <summary>玩家座位的只读视图（M5-T8）。</summary>
    public interface IReadOnlyPlayerState
    {
        int Id { get; }

        IReadOnlyHeroState Hero { get; }

        IReadOnlyManaPool Mana { get; }

        IReadOnlyZone Deck { get; }

        IReadOnlyZone Hand { get; }

        IReadOnlyZone Board { get; }

        IReadOnlyZone Graveyard { get; }

        int FatigueCounter { get; }
    }

    /// <summary>
    /// 对局状态的只读视图（M5-T8 ★）：表现层/PVP 客户端的对局读取唯一入口。
    /// 零拷贝活视图——读取实时映射权威状态；不含 <c>NextInstanceId</c> 等写路径成员。
    /// </summary>
    public interface IReadOnlyMatchState
    {
        TurnPhase Phase { get; }

        int TurnNumber { get; }

        int ActivePlayerId { get; }

        bool IsFinished { get; }

        IReadOnlyPlayerState ActivePlayer { get; }

        IReadOnlyCollection<IReadOnlyPlayerState> Players { get; }

        IReadOnlyPlayerState GetPlayer(int playerId);
    }
}
