using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>step 应用结果种类（M13-T4，Docs/05 §5.2 规则 1）。</summary>
    public enum StepApplyStatus
    {
        Applied = 0,
        Rejected = 1,
        DuplicateDiscarded = 2,
        VersionGap = 3
    }

    /// <summary>step 应用结果：状态裁决 + 拒绝原因 + 事件原文（仅 Applied 携带，防重复播放）。</summary>
    public sealed class StepOutcome
    {
        public StepOutcome(StepApplyStatus status, bool accepted, int? seq,
            CommandError error, string detail, IReadOnlyList<JsonValue> events)
        {
            Status = status;
            Accepted = accepted;
            Seq = seq;
            Error = error;
            Detail = detail ?? string.Empty;
            Events = events;
        }

        /// <summary>应用裁决。</summary>
        public StepApplyStatus Status { get; }

        /// <summary>权威是否接受该命令（拒绝步含原因）。</summary>
        public bool Accepted { get; }

        /// <summary>命令序号回显（命令带 seq 才有）。</summary>
        public int? Seq { get; }

        /// <summary>拒绝错误码（Accepted 为 false 时有效）。</summary>
        public CommandError Error { get; }

        /// <summary>拒绝/结果说明（表现层提示用）。</summary>
        public string Detail { get; }

        /// <summary>事件 JSON 原文（供表现层播动画；非 Applied 结果为空）。</summary>
        public IReadOnlyList<JsonValue> Events { get; }
    }

    /// <summary>
    /// 客户端状态同步器（M13-T4）：以快照为基线，按版本裁决应用权威下发的 step（StateDelta）。
    /// 规则（Docs/05 §5.2）：version ≤ 本地 → 重复包丢弃；basedOn ≠ 本地 → 版本空洞不猜合并；
    /// basedOn == 本地且接受 → <see cref="StateChangeApplier"/> 应用增量并推进本地版本；
    /// 拒绝步不动状态与版本，原因透传表现层。纯 BCL、无网络依赖（帧化归 Card.Network）。
    /// </summary>
    public sealed class ClientStateSync
    {
        private MatchState? _state;
        private ulong _version;

        /// <summary>本地已应用到的权威版本（快照版本或最后应用的 step 版本）。</summary>
        public ulong LocalVersion => _version;

        /// <summary>本地视图状态（须先 ApplySnapshot）。</summary>
        public MatchState State => _state
            ?? throw new InvalidOperationException("ClientStateSync 尚未载入快照，须先 ApplySnapshot。");

        /// <summary>载入快照基线（{ version, state }；可重复调用 = 重置视图，用于 T5 回退）。</summary>
        public void ApplySnapshot(JsonValue snapshotJson)
        {
            Guard.NotNull(snapshotJson, nameof(snapshotJson));
            ulong version = RequireNumber(snapshotJson, "version", "snapshot");
            JsonValue stateJson = RequireMember(snapshotJson, "state", "snapshot");
            _state = MatchStateSerializer.Deserialize(stateJson.ToJson());
            _version = version;
        }

        /// <summary>应用一条 step（StateDelta）；结构非法抛 FormatException（明确失败，不吞）。</summary>
        public StepOutcome ApplyStep(JsonValue step)
        {
            Guard.NotNull(step, nameof(step));
            RequireKind(step, "step");

            ulong version = RequireNumber(step, "version", "step");
            ulong basedOn = RequireNumber(step, "basedOn", "step");
            int? seq = TryReadSeq(step);
            CommandError error = ReadError(step);
            IReadOnlyList<JsonValue> events = ReadEvents(step);

            if (!ReadAccepted(step))
            {
                return new StepOutcome(StepApplyStatus.Rejected, false, seq, error, ReadDetail(step), Empty);
            }

            if (version <= _version)
            {
                return new StepOutcome(StepApplyStatus.DuplicateDiscarded, true, seq, error, ReadDetail(step), Empty);
            }

            if (basedOn != _version)
            {
                return new StepOutcome(StepApplyStatus.VersionGap, true, seq, error, ReadDetail(step), Empty);
            }

            EnsureSnapshot();
            IReadOnlyList<StateChange> changes = StateChangeSerializer.ReadList(RequireMember(step, "changes", "step"));
            StateChangeApplier.Apply(_state!, changes);
            _version = version;
            return new StepOutcome(StepApplyStatus.Applied, true, seq, error, ReadDetail(step), events);
        }

        // -----------------------------------------------------------------
        // 私有：字段读取
        // -----------------------------------------------------------------

        private static readonly IReadOnlyList<JsonValue> Empty = Array.Empty<JsonValue>();

        private static void RequireKind(JsonValue step, string expected)
        {
            if (!step.TryGetMember("kind", out JsonValue kind) || kind.Kind != JsonKind.String
                || kind.StringValue != expected)
            {
                throw new FormatException("step 消息缺少 kind 或不是 \"" + expected + "\"。");
            }
        }

        private void EnsureSnapshot()
        {
            if (_state == null)
            {
                throw new InvalidOperationException("ClientStateSync 尚未载入快照，须先 ApplySnapshot。");
            }
        }

        private static ulong RequireNumber(JsonValue json, string key, string source)
        {
            JsonValue member = RequireMember(json, key, source);
            if (member.Kind != JsonKind.Number)
            {
                throw new FormatException(source + "." + key + " 必须是数字。");
            }

            return (ulong)member.IntValue;
        }

        private static JsonValue RequireMember(JsonValue json, string key, string source)
        {
            if (!json.TryGetMember(key, out JsonValue member))
            {
                throw new FormatException(source + " 缺少必需字段 \"" + key + "\"。");
            }

            return member;
        }

        private static bool ReadAccepted(JsonValue step)
        {
            JsonValue member = RequireMember(step, "accepted", "step");
            if (member.Kind != JsonKind.Bool)
            {
                throw new FormatException("step.accepted 必须是布尔值。");
            }

            return member.BoolValue;
        }

        private static CommandError ReadError(JsonValue step)
        {
            string name = RequireMember(step, "error", "step").StringValue;
            if (!Enum.TryParse(name, ignoreCase: false, out CommandError error))
            {
                throw new FormatException("step.error 未知错误码 \"" + name + "\"。");
            }

            return error;
        }

        private static string ReadDetail(JsonValue step)
            => step.TryGetMember("detail", out JsonValue detail) ? detail.StringValue : string.Empty;

        private static int? TryReadSeq(JsonValue step)
            => step.TryGetMember("seq", out JsonValue seq) && seq.Kind == JsonKind.Number
                ? seq.IntValue
                : (int?)null;

        private static IReadOnlyList<JsonValue> ReadEvents(JsonValue step)
        {
            JsonValue member = RequireMember(step, "events", "step");
            if (member.Kind != JsonKind.Array)
            {
                throw new FormatException("step.events 必须是数组。");
            }

            List<JsonValue> items = new List<JsonValue>(member.Items.Count);
            for (int i = 0; i < member.Items.Count; i++)
            {
                items.Add(member.Items[i]);
            }

            return items;
        }
    }
}
