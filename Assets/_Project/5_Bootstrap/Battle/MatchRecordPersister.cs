using System;
using System.IO;
using Card.Application.Match;
using Card.Core;
using UnityEngine;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// MatchRecord 本地持久化（M14-T5）：对局结束时把 MatchRecord 序列化
    /// 写入 persistentDataPath/MatchRecords/&lt;matchId&gt;.json。
    /// 只有房主侧（或单机 PVE）持有完整 MatchController 命令流水，
    /// 成员侧不生成记录（它只有快照，没有 History）。
    /// </summary>
    internal static class MatchRecordPersister
    {
        private const string FolderName = "MatchRecords";

        /// <summary>保存一条 MatchRecord 到磁盘；目录不存在则创建。</summary>
        public static void Save(MatchRecord record, string? endReasonNet = null)
        {
            Guard.NotNull(record, nameof(record));

            string dir = Path.Combine(UnityEngine.Application.persistentDataPath, FolderName);
            Directory.CreateDirectory(dir);

            // 如果传入了网络结束原因，克隆一份带正确原因的 MatchRecord 再序列化
            MatchRecord toSave = endReasonNet != null
                ? new MatchRecord(record.Outcome, record.Steps, record.PlayerNames, endReasonNet)
                : record;

            string json = MatchRecordSerializer.Serialize(toSave);
            string path = Path.Combine(dir, toSave.Outcome.MatchId + ".json");
            File.WriteAllText(path, json);

            GameLog.Info(LogChannel.Boot, "MatchRecord 已保存：" + path
                + "（steps=" + toSave.Steps.Count.ToString()
                + " duration=" + toSave.Outcome.DurationTicks.ToString() + " ticks）");
        }

        /// <summary>尝试保存：未终局或已保存过则跳过。</summary>
        public static bool TrySave(MatchController controller, ref bool savedFlag, string? endReasonNet = null)
        {
            if (savedFlag || controller.LastRecord == null)
                return false;

            Save(controller.LastRecord, endReasonNet);
            savedFlag = true;
            return true;
        }
    }
}
