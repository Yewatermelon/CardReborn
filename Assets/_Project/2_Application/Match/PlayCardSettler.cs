using System;
using System.Collections.Generic;
using Card.Application.Match.Effects;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match
{
    /// <summary>
    /// 出牌结算器（M4-T4；Docs/01 §3.2 + §4）：消费法力 → 手牌移除 →
    /// 随从进场 / 法术入坟场 → 执行战吼效果（<see cref="CardDefinition.Effects"/>）→
    /// 产出 <see cref="CardPlayedEvent"/>。校验由 RuleEngine 在入队前完成，
    /// 结算期失败属装配错误直接抛。
    /// </summary>
    public sealed class PlayCardSettler : ICommandSettler
    {
        private readonly EffectExecutor _effects = new EffectExecutor();

        public bool CanSettle(IGameCommand command) => command is PlayCardCommand;

        public void Settle(SettlementContext context, IGameCommand command)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(command, nameof(command));

            PlayCardCommand cmd = (PlayCardCommand)command;
            PlayerState player = context.State.GetPlayer(cmd.PlayerId);

            CardInstance? card = FindInHand(player, cmd.CardInstanceId);
            if (card == null)
            {
                throw new InvalidOperationException("出牌结算找不到手牌实例：" + cmd.CardInstanceId);
            }

            CardDefinition definition = context.Database.RequireCard(card.CardKey);

            Result spend = player.Mana.Spend(definition.Cost);
            if (spend.IsFailure)
            {
                throw new InvalidOperationException("出牌法力扣除失败：" + spend.ErrorCode);
            }

            player.Hand.Remove(card);

            if (definition.Type == CardType.Minion)
            {
                Result boardAdd = player.Board.Add(card);
                if (boardAdd.IsFailure)
                {
                    throw new InvalidOperationException("随从进场失败：" + boardAdd.ErrorCode);
                }
            }
            else
            {
                player.Graveyard.Add(card);
            }

            IReadOnlyList<IEffectData> effects = EffectParser.Parse(definition.Effects);
            if (effects.Count > 0)
            {
                EffectContext effectContext = new EffectContext(
                    context.State, context.Database, context.Events, card, cmd.PlayerId, cmd.Target);
                for (int i = 0; i < effects.Count; i++)
                {
                    _effects.Execute(effects[i], effectContext);
                }
            }

            context.Events.Emit(new CardPlayedEvent(cmd.PlayerId, card.InstanceId));
        }

        private static CardInstance? FindInHand(PlayerState player, int instanceId)
        {
            for (int i = 0; i < player.Hand.Count; i++)
            {
                if (player.Hand.Cards[i].InstanceId == instanceId)
                {
                    return player.Hand.Cards[i];
                }
            }

            return null;
        }
    }
}
