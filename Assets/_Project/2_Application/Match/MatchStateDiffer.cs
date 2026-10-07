using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 状态增量计算（M3-T10；FR-13.5，Docs/02 第 150 行）：逐字段比较两个 MatchState，
    /// 产出确定性顺序的 StateChange 列表。路径键与 MatchStateSerializer 一致；纯 BCL，无副作用。
    /// 分区按实例 Id 集合比较（牌库顺序不视为变更，FR-14.5）；跨区移动天然产出旧区 Removed + 新区 Added。
    /// </summary>
    public static class MatchStateDiffer
    {
        public static IReadOnlyList<StateChange> Diff(MatchState before, MatchState after)
        {
            Guard.NotNull(before, nameof(before));
            Guard.NotNull(after, nameof(after));

            List<StateChange> changes = new List<StateChange>();
            DiffRoot(before, after, changes);
            DiffPlayer(before.GetPlayer(0), after.GetPlayer(0), 0, changes);
            DiffPlayer(before.GetPlayer(1), after.GetPlayer(1), 1, changes);
            return changes;
        }

        private static void DiffRoot(MatchState before, MatchState after, List<StateChange> changes)
        {
            DiffInt((int)before.Phase, (int)after.Phase, "phase", changes);
            DiffInt(before.TurnNumber, after.TurnNumber, "turnNumber", changes);
            DiffInt(before.ActivePlayerId, after.ActivePlayerId, "activePlayerId", changes);
            DiffBool(before.IsFinished, after.IsFinished, "isFinished", changes);
        }

        private static void DiffPlayer(PlayerState before, PlayerState after, int index, List<StateChange> changes)
        {
            string prefix = "players[" + index + "]";
            DiffInt(before.FatigueCounter, after.FatigueCounter, prefix + ".fatigueCounter", changes);

            DiffInt(before.Hero.Health, after.Hero.Health, prefix + ".hero.health", changes);
            DiffInt(before.Hero.Armor, after.Hero.Armor, prefix + ".hero.armor", changes);
            DiffBool(before.Hero.PowerUsedThisTurn, after.Hero.PowerUsedThisTurn, prefix + ".hero.powerUsedThisTurn", changes);

            DiffInt(before.Mana.Max, after.Mana.Max, prefix + ".mana.max", changes);
            DiffInt(before.Mana.Current, after.Mana.Current, prefix + ".mana.current", changes);

            DiffZone(before.Deck, after.Deck, prefix + ".deck", changes);
            DiffZone(before.Hand, after.Hand, prefix + ".hand", changes);
            DiffZone(before.Board, after.Board, prefix + ".board", changes);
            DiffZone(before.Graveyard, after.Graveyard, prefix + ".graveyard", changes);
        }

        private static void DiffZone(Zone before, Zone after, string path, List<StateChange> changes)
        {
            Dictionary<int, CardInstance> beforeById = IndexById(before);
            Dictionary<int, CardInstance> afterById = IndexById(after);

            for (int i = 0; i < before.Count; i++)
            {
                CardInstance card = before.Cards[i];
                if (!afterById.ContainsKey(card.InstanceId))
                {
                    changes.Add(new StateChange(ChangeKind.Removed, path, JsonValue.From(card.InstanceId), JsonValue.Null()));
                }
            }

            for (int i = 0; i < after.Count; i++)
            {
                CardInstance card = after.Cards[i];
                if (!beforeById.TryGetValue(card.InstanceId, out CardInstance? oldCard))
                {
                    changes.Add(new StateChange(ChangeKind.Added, path, JsonValue.Null(), MatchStateSerializer.WriteCard(card)));
                    continue;
                }

                DiffCard(oldCard, card, path + "[" + i + "]", changes);
            }
        }

        private static Dictionary<int, CardInstance> IndexById(Zone zone)
        {
            Dictionary<int, CardInstance> index = new Dictionary<int, CardInstance>(zone.Count);
            for (int i = 0; i < zone.Count; i++)
            {
                index[zone.Cards[i].InstanceId] = zone.Cards[i];
            }

            return index;
        }

        private static void DiffCard(CardInstance before, CardInstance after, string path, List<StateChange> changes)
        {
            DiffInt(before.Attack, after.Attack, path + ".attack", changes);
            DiffInt(before.Health, after.Health, path + ".health", changes);
            DiffInt((int)before.Keywords.Flags, (int)after.Keywords.Flags, path + ".keywords", changes);
            DiffInt((int)before.Statuses.Flags, (int)after.Statuses.Flags, path + ".statuses", changes);
            DiffInt(before.AttacksUsedThisTurn, after.AttacksUsedThisTurn, path + ".attacksUsedThisTurn", changes);
        }

        private static void DiffInt(int before, int after, string path, List<StateChange> changes)
        {
            if (before != after)
            {
                changes.Add(new StateChange(ChangeKind.Modified, path, JsonValue.From(before), JsonValue.From(after)));
            }
        }

        private static void DiffBool(bool before, bool after, string path, List<StateChange> changes)
        {
            if (before != after)
            {
                changes.Add(new StateChange(ChangeKind.Modified, path, JsonValue.From(before), JsonValue.From(after)));
            }
        }
    }
}
