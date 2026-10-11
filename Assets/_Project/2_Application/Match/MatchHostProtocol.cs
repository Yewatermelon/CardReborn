using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// MatchHost 上/下行消息编解码（M13-T3 自 MatchHost 拆出，保持单文件 ≤300 行）：
    /// kind 读取、命令载荷解析、快照与 step 构造、事件序列化。无状态纯函数。
    /// step 消息（M13-T4 起即 Docs/05 §5.2 StateDelta）：
    /// { kind, version, basedOn, seq(仅当命令带 seq), accepted, error, detail, finished, changes[], events[] }。
    /// 快照：{ kind, version, state }——version 为产生快照的权威版本（客户端增量基线）。
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

        /// <summary>构造开局快照；version = 产生快照的权威版本（M13-T4：客户端增量基线）。</summary>
        public static JsonValue WriteSnapshot(MatchState state, ulong version)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("snapshot"))
                .Add("version", JsonValue.From((int)version))
                .Add("state", MatchStateSerializer.WriteStateTree(state));
        }

        /// <summary>
        /// 构造 step 回执（StateDelta）；seq 仅在命令携带序号时输出（协议"只加不删"的可选字段）。
        /// basedOn = 增量所基于的权威版本（接受时 = version − 1；拒绝时状态未变 = version）。
        /// </summary>
        public static JsonValue WriteStep(
            ulong version,
            ulong basedOn,
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
                .Add("version", JsonValue.From((int)version))
                .Add("basedOn", JsonValue.From((int)basedOn));
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

        // -----------------------------------------------------------------
        // 命令信封辅助（M13-T3，自 MatchHost 挪入保单文件 ≤300 行）
        // -----------------------------------------------------------------

        /// <summary>读取信封 seq：缺省 = 无序号模式（true + null）；存在但非 ≥1 整数 = false。</summary>
        public static bool TryReadSeq(JsonValue json, out int? seq)
        {
            if (!json.TryGetMember("seq", out JsonValue value))
            {
                seq = null;
                return true;
            }

            if (value.Kind == JsonKind.Number && value.IntValue >= 1)
            {
                seq = value.IntValue;
                return true;
            }

            seq = null;
            return false;
        }

        /// <summary>提取 payload 原文（幂等比对键：载荷逐字节一致才算重复包）。</summary>
        public static string ExtractPayloadJson(JsonValue envelope)
        {
            return envelope.TryGetMember("payload", out JsonValue payload)
                ? payload.ToJson()
                : string.Empty;
        }
    }
}
