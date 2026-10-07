using System;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// StateChangeApplier 路径解析与值校验辅助（M4-T10 拆分文件）。
    /// </summary>
    public static partial class StateChangeApplier
    {
        // -----------------------------------------------------------------
        // 路径解析辅助
        // -----------------------------------------------------------------

        internal static bool TryParsePlayerPrefix(string path, out int seat, out string rest)
        {
            seat = -1;
            rest = string.Empty;
            if (!path.StartsWith("players[", StringComparison.Ordinal))
            {
                return false;
            }

            int close = path.IndexOf(']', 8);
            if (close < 0 || close + 1 >= path.Length || path[close + 1] != '.')
            {
                return false;
            }

            string seatStr = path.Substring(8, close - 8);
            if (!int.TryParse(seatStr, out seat) || (seat != 0 && seat != 1))
            {
                return false;
            }

            rest = path.Substring(close + 2);
            return true;
        }

        internal static bool TryParsePlayerZone(string path, out int seat, out string zoneName)
        {
            seat = -1;
            zoneName = string.Empty;
            if (!TryParsePlayerPrefix(path, out seat, out string rest))
            {
                return false;
            }

            if (rest.IndexOf('.') >= 0 || rest.IndexOf('[') >= 0)
            {
                return false;
            }

            zoneName = rest;
            return true;
        }

        internal static bool TryParsePlayerZoneIndexField(
            string path, out int seat, out string zoneName, out int index, out string field)
        {
            seat = -1;
            zoneName = string.Empty;
            index = -1;
            field = string.Empty;
            if (!TryParsePlayerPrefix(path, out seat, out string rest))
            {
                return false;
            }

            int bracket = rest.IndexOf('[');
            if (bracket < 0)
            {
                return false;
            }

            zoneName = rest.Substring(0, bracket);
            int close = rest.IndexOf(']', bracket);
            if (close < 0 || close + 1 >= rest.Length || rest[close + 1] != '.')
            {
                return false;
            }

            string indexStr = rest.Substring(bracket + 1, close - bracket - 1);
            if (!int.TryParse(indexStr, out index))
            {
                return false;
            }

            field = rest.Substring(close + 2);
            return true;
        }

        internal static Zone ResolveZone(PlayerState player, string zoneName, string path)
        {
            switch (zoneName)
            {
                case "deck": return player.Deck;
                case "hand": return player.Hand;
                case "board": return player.Board;
                case "graveyard": return player.Graveyard;
                default:
                    throw new FormatException("未知分区名：" + zoneName + "（路径 " + path + "）。");
            }
        }

        internal static CardInstance? FindById(Zone zone, int instanceId)
        {
            for (int i = 0; i < zone.Count; i++)
            {
                if (zone.Cards[i].InstanceId == instanceId)
                {
                    return zone.Cards[i];
                }
            }

            return null;
        }

        // -----------------------------------------------------------------
        // 值类型辅助
        // -----------------------------------------------------------------

        internal static int RequireInt(JsonValue value, string path)
        {
            if (value.Kind != JsonKind.Number)
            {
                throw new FormatException(path + " 必须是整数，实际 " + value.Kind + "。");
            }

            return value.IntValue;
        }

        internal static bool RequireBool(JsonValue value, string path)
        {
            if (value.Kind != JsonKind.Bool)
            {
                throw new FormatException(path + " 必须是布尔，实际 " + value.Kind + "。");
            }

            return value.BoolValue;
        }

        internal static T ParseEnum<T>(JsonValue value, string path) where T : struct
        {
            if (value.Kind == JsonKind.Number)
            {
                return (T)(object)value.IntValue;
            }

            if (value.Kind != JsonKind.String)
            {
                throw new FormatException(path + " 必须是字符串或整数枚举，实际 " + value.Kind + "。");
            }

            if (!Enum.TryParse(value.StringValue, ignoreCase: false, out T result))
            {
                throw new FormatException(path + " 含未知枚举名 \"" + value.StringValue + "\"。");
            }

            return result;
        }
    }
}
