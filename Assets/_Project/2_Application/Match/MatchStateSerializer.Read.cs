using System;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>MatchStateSerializer 读取侧（M3-T9）：JsonValue 树 → 全新状态对象。</summary>
    public static partial class MatchStateSerializer
    {
        private static MatchState ReadState(JsonValue root)
        {
            RequireKind(root, JsonKind.Object, "$");
            int version = RequireInt(root, "version", "$");
            if (version != FormatVersion)
            {
                throw new NotSupportedException(
                    "不支持的快照版本: " + version + "（当前支持 " + FormatVersion + "）。");
            }

            JsonValue players = RequireKind(root, JsonKind.Array, "players", "$");
            if (players.Items.Count != 2)
            {
                throw new FormatException("players 必须恰好包含 2 个座位，实际 " + players.Items.Count + "。");
            }

            PlayerState p0 = ReadPlayer(players.Items[0], expectedId: 0);
            PlayerState p1 = ReadPlayer(players.Items[1], expectedId: 1);

            int active = RequireInt(root, "activePlayerId", "$");
            if (active != 0 && active != 1)
            {
                throw new FormatException("activePlayerId 必须为 0 或 1，实际 " + active + "。");
            }

            MatchState state = new MatchState(p0, p1, active)
            {
                Phase = ReadEnum<TurnPhase>(root, "phase", "$"),
                TurnNumber = RequireInt(root, "turnNumber", "$"),
                IsFinished = RequireBool(root, "isFinished", "$")
            };
            return state;
        }

        private static PlayerState ReadPlayer(JsonValue json, int expectedId)
        {
            string path = "players[" + expectedId + "]";
            RequireKind(json, JsonKind.Object, path);

            int id = RequireInt(json, "id", path);
            if (id != expectedId)
            {
                throw new FormatException(path + ".id 必须为 " + expectedId + "，实际 " + id + "。");
            }

            HeroState hero = ReadHero(RequireKind(json, JsonKind.Object, "hero", path));
            JsonValue mana = RequireKind(json, JsonKind.Object, "mana", path);
            ManaPool manaPool = new ManaPool(
                RequireInt(mana, "max", path + ".mana"),
                RequireInt(mana, "current", path + ".mana"));

            PlayerState player = new PlayerState(id, hero, manaPool)
            {
                FatigueCounter = RequireInt(json, "fatigueCounter", path)
            };

            JsonValue zones = RequireKind(json, JsonKind.Object, "zones", path);
            ReadZoneInto(zones, "deck", player.Deck, path);
            ReadZoneInto(zones, "hand", player.Hand, path);
            ReadZoneInto(zones, "board", player.Board, path);
            ReadZoneInto(zones, "graveyard", player.Graveyard, path);
            return player;
        }

        private static HeroState ReadHero(JsonValue json)
        {
            string path = "hero";
            string heroKey = RequireString(json, "heroKey", path);
            string powerKey = RequireString(json, "heroPowerKey", path);
            int maxHealth = RequireInt(json, "maxHealth", path);

            HeroState hero = new HeroState(heroKey, powerKey, maxHealth)
            {
                Health = RequireInt(json, "health", path),
                Armor = RequireInt(json, "armor", path),
                PowerUsedThisTurn = RequireBool(json, "powerUsedThisTurn", path)
            };
            return hero;
        }

        private static void ReadZoneInto(JsonValue zones, string key, Zone target, string parentPath)
        {
            string path = parentPath + ".zones." + key;
            JsonValue array = RequireKind(zones, JsonKind.Array, key, parentPath + ".zones");
            for (int i = 0; i < array.Items.Count; i++)
            {
                target.Add(ReadCard(array.Items[i], path + "[" + i + "]"));
            }
        }

        /// <summary>JsonValue → 单卡（M4-T10 起 internal：StateChangeApplier 的 Added 载荷复用同构格式）。</summary>
        internal static CardInstance ReadCard(JsonValue json, string path)
        {
            RequireKind(json, JsonKind.Object, path);
            return CardInstance.Restore(
                RequireInt(json, "instanceId", path),
                RequireString(json, "cardKey", path),
                RequireInt(json, "ownerId", path),
                RequireInt(json, "attack", path),
                RequireInt(json, "maxHealth", path),
                RequireInt(json, "health", path),
                (Keyword)RequireInt(json, "keywords", path),
                (StatusFlags)RequireInt(json, "statuses", path),
                RequireInt(json, "attacksUsedThisTurn", path));
        }

        private static T ReadEnum<T>(JsonValue json, string key, string path) where T : struct
        {
            string name = RequireString(json, key, path);
            if (!Enum.TryParse(name, ignoreCase: false, out T value))
            {
                throw new FormatException(path + "." + key + " 含未知枚举名 \"" + name + "\"。");
            }

            return value;
        }

        private static int RequireInt(JsonValue json, string key, string path)
        {
            JsonValue member = RequireMember(json, key, path);
            if (member.Kind != JsonKind.Number)
            {
                throw new FormatException(path + "." + key + " 必须是整数。");
            }

            return member.IntValue;
        }

        private static string RequireString(JsonValue json, string key, string path)
        {
            JsonValue member = RequireMember(json, key, path);
            if (member.Kind != JsonKind.String)
            {
                throw new FormatException(path + "." + key + " 必须是字符串。");
            }

            return member.StringValue;
        }

        private static bool RequireBool(JsonValue json, string key, string path)
        {
            JsonValue member = RequireMember(json, key, path);
            if (member.Kind != JsonKind.Bool)
            {
                throw new FormatException(path + "." + key + " 必须是布尔值。");
            }

            return member.BoolValue;
        }

        private static JsonValue RequireKind(JsonValue json, JsonKind kind, string path)
        {
            if (json.Kind != kind)
            {
                throw new FormatException(path + " 必须是 " + kind + "，实际 " + json.Kind + "。");
            }

            return json;
        }

        private static JsonValue RequireKind(JsonValue json, JsonKind kind, string key, string path)
        {
            return RequireKind(RequireMember(json, key, path), kind, path + "." + key);
        }

        private static JsonValue RequireMember(JsonValue json, string key, string path)
        {
            if (!json.TryGetMember(key, out JsonValue member))
            {
                throw new FormatException(path + " 缺少必需字段 \"" + key + "\"。");
            }

            return member;
        }
    }
}
