using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// 对局权威状态根：只存数据（Docs/03 §5.9），无 Unity/UI 引用。
    /// 玩家按 Id 字典索引；所有规则结算（M3-T5 起）围绕本对象进行。
    /// </summary>
    public sealed class MatchState : IReadOnlyMatchState
    {
        private readonly Dictionary<int, PlayerState> _players;

        public MatchState(PlayerState first, PlayerState second, int activePlayerId)
        {
            Guard.NotNull(first, nameof(first));
            Guard.NotNull(second, nameof(second));
            Guard.Require(
                first.Id != second.Id,
                nameof(second),
                "两名玩家的座位 Id 不能相同。");

            _players = new Dictionary<int, PlayerState> { [first.Id] = first, [second.Id] = second };
            ActivePlayerId = RequireKnownSeat(activePlayerId);
        }

        /// <summary>当前回合阶段（默认 <see cref="TurnPhase.MatchStart"/>）。</summary>
        public TurnPhase Phase { get; set; } = TurnPhase.MatchStart;

        /// <summary>回合序号，从 1 开始。</summary>
        public int TurnNumber { get; set; } = 1;

        /// <summary>当前行动方座位 Id。</summary>
        public int ActivePlayerId { get; set; }

        /// <summary>对局是否已结束（M3-T7）。</summary>
        public bool IsFinished { get; set; }

        /// <summary>
        /// 下一个可用的卡牌实例 Id。开局由 MatchFactory 设为 2*DeckSize+1
        /// （避开起手牌库与幸运币的分段 Id），运行时召唤/生成卡牌时递增。
        /// </summary>
        public int NextInstanceId { get; set; }

        /// <summary>分配一个全场唯一的卡牌实例 Id 并递增计数器。</summary>
        public int AllocateInstanceId()
        {
            return NextInstanceId++;
        }

        /// <summary>当前行动方。</summary>
        public PlayerState ActivePlayer => GetPlayer(ActivePlayerId);

        /// <summary>全部玩家（无序，按 Id 用 <see cref="GetPlayer"/> 查询）。</summary>
        public IReadOnlyCollection<PlayerState> Players => _players.Values;

        /// <summary>按座位 Id 取玩家；未知 Id 抛 <see cref="System.ArgumentException"/>。</summary>
        public PlayerState GetPlayer(int playerId)
        {
            if (_players.TryGetValue(playerId, out PlayerState? player))
            {
                return player;
            }

            throw new ArgumentException("未知的玩家座位 Id：" + playerId, nameof(playerId));
        }

        IReadOnlyPlayerState IReadOnlyMatchState.ActivePlayer => ActivePlayer;

        IReadOnlyCollection<IReadOnlyPlayerState> IReadOnlyMatchState.Players => Players;

        IReadOnlyPlayerState IReadOnlyMatchState.GetPlayer(int playerId) => GetPlayer(playerId);

        private int RequireKnownSeat(int playerId)
        {
            GetPlayer(playerId);
            return playerId;
        }
    }
}
