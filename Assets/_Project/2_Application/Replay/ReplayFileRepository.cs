using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Card.Application.Match;
using Card.Core;

namespace Card.Application.Replay
{
    /// <summary>
    /// 回放文件元数据（M15-T1）：仓库列举时随文件返回的索引信息。
    /// </summary>
    public readonly struct ReplayFileMeta
    {
        /// <summary>回放文件绝对路径。</summary>
        public string FilePath { get; }

        /// <summary>录制时的随机种子（与 <see cref="MatchRecording.Seed"/> 一致）。</summary>
        public int Seed { get; }

        /// <summary>录制包含的命令条数。</summary>
        public int CommandCount { get; }

        /// <summary>文件创建时间（来自文件系统）。</summary>
        public DateTime CreatedAt { get; }

        public ReplayFileMeta(string filePath, int seed, int commandCount, DateTime createdAt)
        {
            FilePath = filePath;
            Seed = seed;
            CommandCount = commandCount;
            CreatedAt = createdAt;
        }
    }

    /// <summary>
    /// 回放文件仓库（M15-T1 ★）：把 <see cref="MatchRecording"/> 序列化到磁盘目录，
    /// 提供列举/加载/删除能力。纯 BCL（System.IO），不依赖 UnityEngine。
    /// 文件名格式：<c>rec-&lt;yyyyMMddHHmmssfff&gt;-seed&lt;seed&gt;-&lt;guid8&gt;.json</c>。
    /// 毫秒时间戳 + GUID 短缀共同保证连续 Save 文件名不冲突。
    /// </summary>
    public sealed class ReplayFileRepository
    {
        private const string FilePrefix = "rec-";
        private const string FileSuffix = ".json";

        /// <summary>
        /// 把录制写入 <paramref name="dirPath"/> 下的一个新文件，返回写入路径。
        /// 目录不存在会自动创建。<paramref name="createdAtUtc"/> 由调用方提供
        /// （规则三层不直接取系统时间，见 Docs/03 §5.9.2），用于文件名时间戳段（UTC）。
        /// </summary>
        public string Save(MatchRecording recording, string dirPath, DateTime createdAtUtc)
        {
            Guard.NotNull(recording, nameof(recording));
            Guard.NotNullOrWhiteSpace(dirPath, nameof(dirPath));

            Directory.CreateDirectory(dirPath);

            string timestamp = createdAtUtc.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            string guid8 = Guid.NewGuid().ToString("N").Substring(0, 8);
            string fileName = FilePrefix + timestamp + "-seed" + recording.Seed.ToString(
                CultureInfo.InvariantCulture) + "-" + guid8 + FileSuffix;
            string filePath = Path.Combine(dirPath, fileName);

            File.WriteAllText(filePath, recording.ToJson());
            return filePath;
        }

        /// <summary>
        /// 列举目录下所有回放文件元数据。目录不存在返回空集合；
        /// 损坏文件跳过，不影响其他文件列举。
        /// </summary>
        public IReadOnlyList<ReplayFileMeta> List(string dirPath)
        {
            Guard.NotNullOrWhiteSpace(dirPath, nameof(dirPath));

            if (!Directory.Exists(dirPath))
            {
                return Array.Empty<ReplayFileMeta>();
            }

            string[] files = Directory.GetFiles(dirPath, FilePrefix + "*" + FileSuffix);
            var result = new List<ReplayFileMeta>(files.Length);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                try
                {
                    MatchRecording recording = Load(file);
                    DateTime createdAt = File.GetCreationTime(file);
                    result.Add(new ReplayFileMeta(file, recording.Seed, recording.Commands.Count, createdAt));
                }
                catch (ArgumentException)
                {
                    // 损坏文件跳过（损坏 JSON / 缺字段 / 不存在）
                }
                catch (FileNotFoundException)
                {
                    // 并发删除：跳过
                }
            }

            return result;
        }

        /// <summary>
        /// 从磁盘加载回放。文件不存在抛 <see cref="FileNotFoundException"/>；
        /// JSON 解析失败抛 <see cref="ArgumentException"/>。
        /// </summary>
        public MatchRecording Load(string filePath)
        {
            Guard.NotNullOrWhiteSpace(filePath, nameof(filePath));
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("回放文件不存在：" + filePath, filePath);
            }

            string text = File.ReadAllText(filePath);
            try
            {
                return MatchRecording.FromJson(text);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(
                    "回放文件损坏（JSON 解析失败）：" + filePath, nameof(filePath), ex);
            }
        }

        /// <summary>删除回放文件；文件不存在返回 false。</summary>
        public bool Delete(string filePath)
        {
            Guard.NotNullOrWhiteSpace(filePath, nameof(filePath));
            if (!File.Exists(filePath))
            {
                return false;
            }

            File.Delete(filePath);
            return true;
        }
    }
}
