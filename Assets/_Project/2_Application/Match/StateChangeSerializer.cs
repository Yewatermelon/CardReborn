using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// StateChange 列表 ↔ JsonValue（M4-T10）：kind 用枚举名字符串 + Enum.TryParse，
    /// path 为点分路径，old/new 为内嵌 JsonValue（Added 的 OldValue / Removed 的 NewValue 为 Null）。
    /// </summary>
    public static class StateChangeSerializer
    {
        public static JsonValue WriteList(IReadOnlyList<StateChange> changes)
        {
            Guard.NotNull(changes, nameof(changes));
            JsonValue[] items = new JsonValue[changes.Count];
            for (int i = 0; i < changes.Count; i++)
            {
                StateChange c = changes[i];
                items[i] = JsonValue.Object()
                    .Add("kind", JsonValue.From(c.Kind.ToString()))
                    .Add("path", JsonValue.From(c.Path))
                    .Add("old", c.OldValue)
                    .Add("new", c.NewValue);
            }

            return JsonValue.Array(items);
        }

        public static List<StateChange> ReadList(JsonValue json)
        {
            Guard.NotNull(json, nameof(json));
            RequireKind(json, JsonKind.Array, "$");

            List<StateChange> result = new List<StateChange>(json.Items.Count);
            for (int i = 0; i < json.Items.Count; i++)
            {
                result.Add(ReadOne(json.Items[i], "$[" + i + "]"));
            }

            return result;
        }

        private static StateChange ReadOne(JsonValue json, string path)
        {
            RequireKind(json, JsonKind.Object, path);
            string kindName = RequireMember(json, "kind", path).StringValue;
            if (!Enum.TryParse(kindName, ignoreCase: false, out ChangeKind kind))
            {
                throw new FormatException(path + ".kind 未知枚举名 \"" + kindName + "\"。");
            }

            string changePath = RequireMember(json, "path", path).StringValue;
            JsonValue oldValue = RequireMember(json, "old", path);
            JsonValue newValue = RequireMember(json, "new", path);
            return new StateChange(kind, changePath, oldValue, newValue);
        }

        private static JsonValue RequireMember(JsonValue json, string key, string path)
        {
            if (!json.TryGetMember(key, out JsonValue member))
            {
                throw new FormatException(path + " 缺少必需字段 \"" + key + "\"。");
            }

            return member;
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
