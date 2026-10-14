using System;
using System.IO;
using Card.Application.Replay;
using Card.Core;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 对局产物落盘（partial，M14-T5 + M15-T1）：终局时把 MatchRecord 与回放录制
    /// 写入 persistentDataPath；由主文件的 <see cref="Update"/> 每帧驱动、只触发一次。
    /// </summary>
    public sealed partial class BattleSceneBootstrap
    {
        /// <summary>M14-T5：对局终局时持久化 MatchRecord 到磁盘；未终局或已保存过则跳过。</summary>
        private void PersistMatchRecordIfNeeded()
        {
            if (_recordSaved || _controller == null || _controller.LastRecord == null)
                return;

            // 联机房主侧带上网络结束原因（正常终局为 null/EndReasonNormal）
            string? endReasonNet = _netLobby?.HostPeer?.LastMatchEndReason;
            MatchRecordPersister.TrySave(_controller, ref _recordSaved, endReasonNet);
            SaveReplayIfNeeded();
        }

        /// <summary>
        /// M15-T1：对局终局时把命令流水录制写盘（persistentDataPath/Replays）。
        /// 只在本地对局（PVE/热座）有收集器；联机对局收集器为 null 直接跳过。
        /// 无论成败只尝试一次——磁盘异常不应每帧重试。
        /// </summary>
        private void SaveReplayIfNeeded()
        {
            if (_replaySaved || _replayCollector == null)
                return;

            _replaySaved = true;
            try
            {
                string dir = Path.Combine(UnityEngine.Application.persistentDataPath, "Replays");
                string path = new ReplayFileRepository().Save(
                    _replayCollector.Recording, dir, DateTime.UtcNow);
                GameLog.Info(LogChannel.Boot, "回放录制已保存：" + path);
            }
            catch (Exception e)
            {
                GameLog.Warn(LogChannel.Boot, "回放录制保存失败：" + e.Message);
            }
        }
    }
}
