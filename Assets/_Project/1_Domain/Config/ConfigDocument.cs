using System;
using System.Collections.Generic;

namespace Card.Domain.Config
{
    /// <summary>配置文件的信封：版本号 + 条目列表（导入器产出、运行时读取）。</summary>
    /// <typeparam name="TEntry">条目类型（如 CardDefinition）。</typeparam>
    public sealed class ConfigDocument<TEntry>
    {
        /// <summary>schema 版本；默认写入当前版本。</summary>
        public int SchemaVersion { get; init; } = ConfigSchema.CurrentVersion;

        /// <summary>条目列表；默认空集合（不是 null）。</summary>
        public IReadOnlyList<TEntry> Entries { get; init; } = Array.Empty<TEntry>();
    }
}
