using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>MatchStateSerializer 写入侧（M3-T9）：状态 → JsonValue 树。</summary>
    public static partial class MatchStateSerializer
    {
        private static JsonValue WriteState(MatchState state)
        {
            return JsonValue.Object()
                .Add("version", JsonValue.From(FormatVersion))
                .Add("phase", JsonValue.From(state.Phase.ToString()))
                .Add("turnNumber", JsonValue.From(state.TurnNumber))
                .Add("activePlayerId", JsonValue.From(state.ActivePlayerId))
                .Add("isFinished", JsonValue.From(state.IsFinished))
                .Add("players", JsonValue.Array(WritePlayer(state.GetPlayer(0)), WritePlayer(state.GetPlayer(1))));
        }

        private static JsonValue WritePlayer(PlayerState player)
        {
            return JsonValue.Object()
                .Add("id", JsonValue.From(player.Id))
                .Add("fatigueCounter", JsonValue.From(player.FatigueCounter))
                .Add("hero", WriteHero(player.Hero))
                .Add("mana", WriteMana(player.Mana))
                .Add("zones", WriteZones(player));
        }

        private static JsonValue WriteHero(HeroState hero)
        {
            return JsonValue.Object()
                .Add("heroKey", JsonValue.From(hero.HeroKey))
                .Add("heroPowerKey", JsonValue.From(hero.HeroPowerKey))
                .Add("maxHealth", JsonValue.From(hero.MaxHealth))
                .Add("health", JsonValue.From(hero.Health))
                .Add("armor", JsonValue.From(hero.Armor))
                .Add("powerUsedThisTurn", JsonValue.From(hero.PowerUsedThisTurn));
        }

        private static JsonValue WriteMana(ManaPool mana)
        {
            return JsonValue.Object()
                .Add("max", JsonValue.From(mana.Max))
                .Add("current", JsonValue.From(mana.Current));
        }

        private static JsonValue WriteZones(PlayerState player)
        {
            return JsonValue.Object()
                .Add("deck", WriteCards(player.Deck))
                .Add("hand", WriteCards(player.Hand))
                .Add("board", WriteCards(player.Board))
                .Add("graveyard", WriteCards(player.Graveyard));
        }

        private static JsonValue WriteCards(Zone zone)
        {
            List<JsonValue> items = new List<JsonValue>(zone.Count);
            for (int i = 0; i < zone.Count; i++)
            {
                items.Add(WriteCard(zone.Cards[i]));
            }

            return JsonValue.Array(items.ToArray());
        }

        /// <summary>单卡 → JsonValue（M4-T10 起 internal：MatchStateDiffer 的 Added 载荷复用同构格式）。</summary>
        internal static JsonValue WriteCard(CardInstance card)
        {
            return JsonValue.Object()
                .Add("instanceId", JsonValue.From(card.InstanceId))
                .Add("cardKey", JsonValue.From(card.CardKey))
                .Add("ownerId", JsonValue.From(card.OwnerId))
                .Add("attack", JsonValue.From(card.Attack))
                .Add("maxHealth", JsonValue.From(card.MaxHealth))
                .Add("health", JsonValue.From(card.Health))
                .Add("keywords", JsonValue.From((int)card.Keywords.Flags))
                .Add("statuses", JsonValue.From((int)card.Statuses.Flags))
                .Add("attacksUsedThisTurn", JsonValue.From(card.AttacksUsedThisTurn));
        }
    }
}
