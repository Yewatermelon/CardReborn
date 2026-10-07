using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 状态增量应用器（M4-T10）：把 StateChange 列表应用到 MatchState。
    /// 顺序：Removed → Modified → Added（Added 语义 = 分区末尾追加，与 Zone.Add 一致）。
    /// 未知路径 / 结构非法 → 抛 <see cref="FormatException"/>（明确失败，不吞）。
    /// 路径解析与值校验辅助见 <see cref="StateChangeApplier.Paths"/>。
    /// </summary>
    public static partial class StateChangeApplier
    {
        public static void Apply(MatchState state, IReadOnlyList<StateChange> changes)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(changes, nameof(changes));

            ApplyKind(state, changes, ChangeKind.Removed);
            ApplyKind(state, changes, ChangeKind.Modified);
            ApplyKind(state, changes, ChangeKind.Added);
        }

        private static void ApplyKind(MatchState state, IReadOnlyList<StateChange> changes, ChangeKind kind)
        {
            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i].Kind == kind)
                {
                    ApplyOne(state, changes[i]);
                }
            }
        }

        private static void ApplyOne(MatchState state, StateChange change)
        {
            string path = change.Path;

            if (TryApplyRoot(state, change))
            {
                return;
            }

            if (TryApplyPlayerField(state, change))
            {
                return;
            }

            if (TryApplyZoneAddRemove(state, change))
            {
                return;
            }

            if (TryApplyCardField(state, change))
            {
                return;
            }

            throw new FormatException("未知或非法的增量路径：" + path);
        }

        // -----------------------------------------------------------------
        // 根字段
        // -----------------------------------------------------------------

        private static bool TryApplyRoot(MatchState state, StateChange change)
        {
            switch (change.Path)
            {
                case "phase":
                    state.Phase = ParseEnum<TurnPhase>(change.NewValue, change.Path);
                    return true;
                case "turnNumber":
                    state.TurnNumber = RequireInt(change.NewValue, change.Path);
                    return true;
                case "activePlayerId":
                    state.ActivePlayerId = RequireInt(change.NewValue, change.Path);
                    return true;
                case "isFinished":
                    state.IsFinished = RequireBool(change.NewValue, change.Path);
                    return true;
                default:
                    return false;
            }
        }

        // -----------------------------------------------------------------
        // 玩家标量字段（hero / mana / fatigueCounter）
        // -----------------------------------------------------------------

        private static bool TryApplyPlayerField(MatchState state, StateChange change)
        {
            string path = change.Path;
            if (!TryParsePlayerPrefix(path, out int seat, out string rest))
            {
                return false;
            }

            PlayerState player = state.GetPlayer(seat);
            switch (rest)
            {
                case "fatigueCounter":
                    player.FatigueCounter = RequireInt(change.NewValue, path);
                    return true;
                case "hero.health":
                    player.Hero.Health = RequireInt(change.NewValue, path);
                    return true;
                case "hero.armor":
                    player.Hero.Armor = RequireInt(change.NewValue, path);
                    return true;
                case "hero.powerUsedThisTurn":
                    player.Hero.PowerUsedThisTurn = RequireBool(change.NewValue, path);
                    return true;
                case "mana.max":
                    player.Mana.Restore(RequireInt(change.NewValue, path), player.Mana.Current);
                    return true;
                case "mana.current":
                    player.Mana.Restore(player.Mana.Max, RequireInt(change.NewValue, path));
                    return true;
                default:
                    return false;
            }
        }

        // -----------------------------------------------------------------
        // 分区增删（两段路径：players[N].<zone>）
        // -----------------------------------------------------------------

        private static bool TryApplyZoneAddRemove(MatchState state, StateChange change)
        {
            string path = change.Path;
            if (!TryParsePlayerZone(path, out int seat, out string zoneName))
            {
                return false;
            }

            PlayerState player = state.GetPlayer(seat);
            Zone zone = ResolveZone(player, zoneName, path);

            if (change.Kind == ChangeKind.Removed)
            {
                int instanceId = RequireInt(change.OldValue, path);
                CardInstance? card = FindById(zone, instanceId);
                if (card == null)
                {
                    throw new FormatException("Removed 路径 " + path + " 找不到实例 " + instanceId + "。");
                }

                zone.Remove(card);
                return true;
            }

            if (change.Kind == ChangeKind.Added)
            {
                CardInstance card = MatchStateSerializer.ReadCard(change.NewValue, path);
                zone.Add(card);
                return true;
            }

            return false;
        }

        // -----------------------------------------------------------------
        // 卡字段修改（三段路径：players[N].<zone>[i].<field>）
        // -----------------------------------------------------------------

        private static bool TryApplyCardField(MatchState state, StateChange change)
        {
            string path = change.Path;
            if (!TryParsePlayerZoneIndexField(path, out int seat, out string zoneName, out int index, out string field))
            {
                return false;
            }

            PlayerState player = state.GetPlayer(seat);
            Zone zone = ResolveZone(player, zoneName, path);
            if (index < 0 || index >= zone.Count)
            {
                throw new FormatException("路径 " + path + " 索引越界（分区大小 " + zone.Count + "）。");
            }

            CardInstance card = zone.Cards[index];
            switch (field)
            {
                case "attack":
                    card.Attack = RequireInt(change.NewValue, path);
                    return true;
                case "health":
                    card.Health = RequireInt(change.NewValue, path);
                    return true;
                case "keywords":
                    card.Keywords.Clear();
                    card.Keywords.Add((Keyword)RequireInt(change.NewValue, path));
                    return true;
                case "statuses":
                    card.Statuses.Clear();
                    card.Statuses.Add((StatusFlags)RequireInt(change.NewValue, path));
                    return true;
                case "attacksUsedThisTurn":
                    card.AttacksUsedThisTurn = RequireInt(change.NewValue, path);
                    return true;
                default:
                    throw new FormatException("未知卡字段：" + field + "（路径 " + path + "）。");
            }
        }
    }
}
