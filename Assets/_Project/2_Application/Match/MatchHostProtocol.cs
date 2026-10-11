using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// MatchHost 上/下行消息编解码（M13-T3 自 MatchHost 拆出，保持单文件 ≤300 行）：
    /// kind 读取、命令载荷解析、快照与 step 构造、事件序列化。无状态纯函数。
    /// step 消息：{ kind, version, seq(仅当命令带 seq), accepted, error, detail, finished, changes[], events[] }。
    /// </summary>
    internal static class MatchHostProtocol
    {
        /// <summary>读取上行消息 kind；缺失或非字符串抛 FormatException（协议畸形）。</summary>
        public static string ReadKind(JsonValue json)
        {
            if (json.TryGetMember("kind", out JsonValue kind) && kind.Kind == JsonKind.String)
            {
                return kind.StringValue;
            }

            throw new FormatException("上行消息缺少 kind 字段。");
        }

        /// <summary>
        /// 读取 command 消息载荷（M13-T2）：缺 payload / 非法 JSON / 未知类型 / 缺成员
        /// 一律返回 false 并携带可读原因，由调用方产出结构化拒绝。
        /// </summary>
        public static bool TryReadCommand(JsonValue json, out IGameCommand? command, out string? error)
        {
            if (!json.TryGetMember("payload", out JsonValue payload))
            {
                command = null;
                error = "command 消息缺少 payload 字段。";
                return false;
            }

            try
            {
                command = CommandSerializer.Deserialize(payload.ToJson());
                error = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                command = null;
                error = "命令载荷非法：" + ex.Message;
                return false;
            }
            catch (FormatException ex)
            {
                command = null;
                error = "命令载荷不是合法 JSON：" + ex.Message;
                return false;
            }
        }

        public static JsonValue WriteSnapshot(MatchState state)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("snapshot"))
                .Add("state", MatchStateSerializer.WriteStateTree(state));
        }

        /// <summary>构造 step 回执；seq 仅在命令携带序号时输出（协议"只加不删"的可选字段）。</summary>
        public static JsonValue WriteStep(
            ulong version,
            int? seq,
            bool accepted,
            CommandError error,
            string detail,
            bool finished,
            IReadOnlyList<StateChange> changes,
            IReadOnlyList<GameEvent> events)
        {
            JsonValue message = JsonValue.Object()
                .Add("kind", JsonValue.From("step"))
                .Add("version", JsonValue.From((int)version));
            if (seq.HasValue)
            {
                message = message.Add("seq", JsonValue.From(seq.Value));
            }

            return message
                .Add("accepted", JsonValue.From(accepted))
                .Add("error", JsonValue.From(error.ToString()))
                .Add("detail", JsonValue.From(detail))
                .Add("finished", JsonValue.From(finished))
                .Add("changes", StateChangeSerializer.WriteList(changes))
                .Add("events", JsonValue.Array(SerializeEvents(events)));
        }

        private static JsonValue[] SerializeEvents(IReadOnlyList<GameEvent> events)
        {
            JsonValue[] items = new JsonValue[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                items[i] = EventSerializer.WriteEvent(events[i]);
            }

            return items;
        }
    }
}
