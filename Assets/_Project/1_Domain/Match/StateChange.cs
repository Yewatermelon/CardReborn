using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>状态变更种类（M3-T10）：标量修改 / 分区新增 / 分区移除。</summary>
    public enum ChangeKind
    {
        Modified = 0,
        Added = 1,
        Removed = 2
    }

    /// <summary>
    /// 状态增量条目（M3-T10；FR-13.5，纯数据）：<see cref="Path"/> 与序列化器键方案一致
    /// （如 <c>players[0].hero.health</c>、<c>players[1].hand</c>、<c>players[0].board[2].health</c>）。
    /// Added 的 OldValue / Removed 的 NewValue 为 <see cref="JsonValue.Null"/>。
    /// </summary>
    public sealed class StateChange
    {
        public StateChange(ChangeKind kind, string path, JsonValue oldValue, JsonValue newValue)
        {
            Kind = kind;
            Path = Guard.NotNullOrWhiteSpace(path, nameof(path));
            OldValue = Guard.NotNull(oldValue, nameof(oldValue));
            NewValue = Guard.NotNull(newValue, nameof(newValue));
        }

        /// <summary>变更种类。</summary>
        public ChangeKind Kind { get; }

        /// <summary>变更位置（点分路径，与 MatchStateSerializer 的 JSON 键一致）。</summary>
        public string Path { get; }

        /// <summary>变更前的值（Added 时为 null 值）。</summary>
        public JsonValue OldValue { get; }

        /// <summary>变更后的值（Removed 时为 null 值）。</summary>
        public JsonValue NewValue { get; }
    }
}
