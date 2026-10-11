using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 视野裁剪（M13-T6，Docs/05 §5.3 可见性矩阵）：按观察者座位生成只含其可见信息的状态视图。
    /// 对手手牌：数量保留、内容抹除（背面占位卡）；双方牌库：顺序与内容均不下发（自己的也不下发，
    /// 客户端不推演）；board / graveyard / 英雄 / 法力：公开信息完整下发。
    /// 占位卡保留真实 InstanceId（Differ 按 InstanceId 索引，数量增减自动同步），
    /// 内容以 <see cref="MaskedCardKey"/> 背面标识抹除。视图自序列化往返深拷贝而来，绝不修改权威状态。
    /// </summary>
    public static class PlayerViewProjector
    {
        /// <summary>背面占位卡标识（协议哨兵值，非配置卡 Key；表现层据此显示卡背）。</summary>
        public const string MaskedCardKey = "__hidden__";

        /// <summary>按观察者视角裁剪权威状态；非法观察座位按保守策略遮蔽双方手牌。</summary>
        public static MatchState Project(MatchState authoritative, int observerPlayerId)
        {
            Guard.NotNull(authoritative, nameof(authoritative));
            MatchState view = MatchStateSerializer.Deserialize(
                MatchStateSerializer.Serialize(authoritative));

            foreach (PlayerState player in view.Players)
            {
                MaskZone(player.Deck, player.Id);
                if (player.Id != observerPlayerId)
                {
                    MaskZone(player.Hand, player.Id);
                }
            }

            return view;
        }

        /// <summary>把分区内容替换为同序背面占位卡（数量保留；操作对象为深拷贝副本，非权威对象）。</summary>
        private static void MaskZone(Zone zone, int ownerId)
        {
            CardInstance[] originals = new CardInstance[zone.Count];
            for (int i = 0; i < zone.Count; i++)
            {
                originals[i] = zone.Cards[i];
            }

            foreach (CardInstance card in originals)
            {
                zone.Remove(card);
            }

            foreach (CardInstance card in originals)
            {
                zone.Add(CardInstance.Restore(
                    card.InstanceId, MaskedCardKey, ownerId,
                    attack: 0, maxHealth: 0, health: 0,
                    Keyword.None, StatusFlags.None, attacksUsedThisTurn: 0));
            }
        }
    }
}
