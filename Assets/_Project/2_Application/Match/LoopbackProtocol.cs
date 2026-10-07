using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 进程内回环消息编解码（M4-T10；internal static）。
    /// 上行：{"kind":"hello"}、{"kind":"command","payload":<CommandSerializer 对象>}。
    /// 下行：{"kind":"snapshot","version":N,"state":<WriteState 对象>}、
    ///      {"kind":"step","version":N,"accepted":bool,"error":"None|...","finished":bool,"changes":[...]}。
    /// </summary>
    internal static class LoopbackProtocol
    {
        // -----------------------------------------------------------------
        // 上行
        // -----------------------------------------------------------------

        public static JsonValue WriteHello()
        {
            return JsonValue.Object().Add("kind", JsonValue.From("hello"));
        }

        public static JsonValue WriteCommand(IGameCommand command)
        {
            string payload = CommandSerializer.Serialize(command);
            return JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", JsonValue.Parse(payload));
        }

        public static bool TryReadHello(JsonValue json)
        {
            return json.Kind == JsonKind.Object
                && json.TryGetMember("kind", out JsonValue kind)
                && kind.Kind == JsonKind.String
                && kind.StringValue == "hello";
        }

        public static IGameCommand ReadCommand(JsonValue json)
        {
            RequireKind(json, JsonKind.Object, "$");
            string kind = RequireString(json, "kind", "$");
            if (kind != "command")
            {
                throw new FormatException("上行消息 kind 必须为 command，实际 \"" + kind + "\"。");
            }

            JsonValue payload = RequireMember(json, "payload", "$");
            return CommandSerializer.Deserialize(payload.ToJson());
        }

        // -----------------------------------------------------------------
        // 下行
        // -----------------------------------------------------------------

        public static JsonValue WriteSnapshot(int version, MatchState state)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("snapshot"))
                .Add("version", JsonValue.From(version))
                .Add("state", MatchStateSerializer.WriteStateTree(state));
        }

        public static JsonValue WriteStep(
            int version,
            bool accepted,
            CommandError error,
            bool finished,
            IReadOnlyList<StateChange> changes)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("step"))
                .Add("version", JsonValue.From(version))
                .Add("accepted", JsonValue.From(accepted))
                .Add("error", JsonValue.From(error.ToString()))
                .Add("finished", JsonValue.From(finished))
                .Add("changes", StateChangeSerializer.WriteList(changes));
        }

        public static bool TryReadSnapshot(JsonValue json, out int version, out MatchState? state)
        {
            version = 0;
            state = null;
            if (json.Kind != JsonKind.Object
                || !json.TryGetMember("kind", out JsonValue kind)
                || kind.Kind != JsonKind.String
                || kind.StringValue != "snapshot")
            {
                return false;
            }

            version = RequireInt(json, "version", "$");
            JsonValue stateJson = RequireMember(json, "state", "$");
            state = MatchStateSerializer.ReadStateTree(stateJson);
            return true;
        }

        public static bool TryReadStep(
            JsonValue json,
            out int version,
            out bool accepted,
            out CommandError error,
            out bool finished,
            out List<StateChange>? changes)
        {
            version = 0;
            accepted = false;
            error = CommandError.None;
            finished = false;
            changes = null;
            if (json.Kind != JsonKind.Object
                || !json.TryGetMember("kind", out JsonValue kind)
                || kind.Kind != JsonKind.String
                || kind.StringValue != "step")
            {
                return false;
            }

            version = RequireInt(json, "version", "$");
            accepted = RequireBool(json, "accepted", "$");
            string errorName = RequireString(json, "error", "$");
            if (!Enum.TryParse(errorName, ignoreCase: false, out error))
            {
                throw new FormatException("$.error 未知枚举名 \"" + errorName + "\"。");
            }

            finished = RequireBool(json, "finished", "$");
            changes = StateChangeSerializer.ReadList(RequireMember(json, "changes", "$"));
            return true;
        }

        // -----------------------------------------------------------------
        // 辅助
        // -----------------------------------------------------------------

        private static JsonValue RequireMember(JsonValue json, string key, string path)
        {
            if (!json.TryGetMember(key, out JsonValue member))
            {
                throw new FormatException(path + " 缺少必需字段 \"" + key + "\"。");
            }

            return member;
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

        private static bool RequireBool(JsonValue json, string key, string path)
        {
            JsonValue member = RequireMember(json, key, path);
            if (member.Kind != JsonKind.Bool)
            {
                throw new FormatException(path + "." + key + " 必须是布尔值。");
            }

            return member.BoolValue;
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

        private static void RequireKind(JsonValue json, JsonKind kind, string path)
        {
            if (json.Kind != kind)
            {
                throw new FormatException(path + " 必须是 " + kind + "，实际 " + json.Kind + "。");
            }
        }
    }
}
