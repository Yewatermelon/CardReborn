using System;
using System.Collections.Generic;
using System.IO;
using Card.Application.Match;
using Card.Application.Replay;
using Card.Bootstrap.Battle;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Log;
using UnityEngine;

namespace Card.Bootstrap.Replay
{
    /// <summary>
    /// 回放场景引导器（M15-T1）：只读回放——复用战斗 UI 体系渲染
    /// <see cref="ReplayPlayer.CurrentSnapshot"/>，不装配控制器/输入（禁用命令上行）。
    /// 控制条支持播放/暂停/单步/倍速/拖动跳帧/回放文件切换；Finished 后点播放重播。
    /// M6-T1 模式：场景只挂本脚本，<see cref="Start"/> 自包含初始化。
    /// 回放文件取 persistentDataPath/Replays（对局终局由 BattleSceneBootstrap 写入）。
    /// </summary>
    public sealed class ReplaySceneBootstrap : MonoBehaviour
    {
        private const string ConfigFolder = "CardConfig";
        private const string ReplaysFolder = "Replays";
        private const string LocalizationFile = "localization.csv";
        private const float BaseStepInterval = 0.5f; // 1x 每秒推进 2 帧，2x/4x 线性加快

        private CardDatabase? _database;
        private ReplayFileRepository _repository = null!;
        private IReadOnlyList<ReplayFileMeta> _metas = Array.Empty<ReplayFileMeta>();
        private int _metaIndex;
        private ReplayPlayer? _player;
        private BattleViewSynchronizer? _synchronizer;
        private ReplayControlBar? _bar;
        private BattleLogView? _log;
        private int _lastRenderedFrame = -1;
        private int _lastLoggedFrame;
        private float _stepTimer;
        private bool _seekPending; // Seek/重播/切文件时置 true，PushLogEntries 据此清空+全量重投

        private void Start()
        {
            BootLogging.ConfigureOnce();
            SceneCamera.Ensure();

            BattleUi ui = BattleUiFactory.Create();
            string configDir = Path.Combine(UnityEngine.Application.streamingAssetsPath, ConfigFolder);
            (CardDatabase? database, IReadOnlyList<string> errors) = BattleComposition.LoadDatabase(configDir);
            if (database == null)
            {
                GameLog.Error(LogChannel.Boot, "回放场景配置加载失败：" + string.Join("; ", errors));
                return;
            }

            _database = database;
            _synchronizer = new BattleViewSynchronizer(
                ui, database, LoadTextResolver(configDir),
                BattleSceneBootstrap.LocalSeat, BattleSceneBootstrap.EnemySeat,
                observerMode: true);
            _log = ui.Log;
            _repository = new ReplayFileRepository();
            _bar = new ReplayControlBar(ui.CanvasRoot);
            BindControlBar();

            string replaysDir = Path.Combine(UnityEngine.Application.persistentDataPath, ReplaysFolder);
            _metas = _repository.List(replaysDir);
            if (_metas.Count == 0)
            {
                GameLog.Warn(LogChannel.Boot, "回放目录为空（先打一局 PVE/热座终局生成回放）：" + replaysDir);
                return;
            }

            LoadReplay(0);
        }

        private void Update()
        {
            ReplayPlayer? player = _player;
            if (player == null || _synchronizer == null)
            {
                return;
            }

            if (player.State == ReplayPlayerState.Playing)
            {
                float interval = BaseStepInterval / Mathf.Max(player.Speed, 0.01f);
                _stepTimer += Time.deltaTime;
                while (_stepTimer >= interval && player.State == ReplayPlayerState.Playing)
                {
                    player.Step();
                    _stepTimer -= interval;
                }
            }

            RenderCurrent();
        }

        private void BindControlBar()
        {
            _bar!.PlayPauseClicked += TogglePlayPause;
            _bar.StepBackClicked += () => StepBy(-1);
            _bar.StepForwardClicked += () => StepBy(+1);
            _bar.SpeedClicked += CycleSpeed;
            _bar.SliderSeekRequested += SeekTo;
            _bar.ReplaySwitched += direction =>
            {
                if (_metas.Count > 0)
                {
                    LoadReplay((_metaIndex + direction + _metas.Count) % _metas.Count);
                }
            };
        }

        private void LoadReplay(int index)
        {
            if (_metas.Count == 0 || _database == null)
            {
                return;
            }

            _metaIndex = Math.Max(0, Math.Min(_metas.Count - 1, index));
            MatchRecording recording = _repository.Load(_metas[_metaIndex].FilePath);
            _player = new ReplayPlayer(recording, _database);
            _lastRenderedFrame = -1;
            _stepTimer = 0f;
            _seekPending = true;
            _lastLoggedFrame = 0;
            _bar?.SetFile(Path.GetFileName(_metas[_metaIndex].FilePath));
            _bar?.SetSpeed(_player.Speed);
            RenderCurrent();
        }

        private void TogglePlayPause()
        {
            ReplayPlayer? player = _player;
            if (player == null)
            {
                return;
            }

            if (player.State == ReplayPlayerState.Playing)
            {
                player.Pause();
            }
            else if (player.State == ReplayPlayerState.Finished)
            {
                player.Seek(0); // 终局后点播放 = 重播
                player.Play();
                _seekPending = true;
            }
            else
            {
                player.Play();
            }

            _bar?.SetPlaying(player.State == ReplayPlayerState.Playing);
        }

        /// <summary>单步前进/后退（后退 = 重建快进到上一帧，Seek 语义）。</summary>
        private void StepBy(int direction)
        {
            ReplayPlayer? player = _player;
            if (player == null)
            {
                return;
            }

            player.Pause();
            int target = Math.Max(0, Math.Min(player.TotalFrames, player.CurrentFrame + direction));
            if (target == player.CurrentFrame)
            {
                return;
            }

            player.Seek(target);
            _seekPending = true;
            _bar?.SetPlaying(false);
        }

        private void CycleSpeed()
        {
            ReplayPlayer? player = _player;
            if (player == null)
            {
                return;
            }

            float next = player.Speed >= 4f ? 1f : (player.Speed >= 2f ? 4f : 2f);
            player.SetSpeed(next);
            _bar?.SetSpeed(next);
        }

        private void SeekTo(int frame)
        {
            ReplayPlayer? player = _player;
            if (player == null)
            {
                return;
            }

            player.Pause();
            player.Seek(Math.Max(0, Math.Min(player.TotalFrames, frame)));
            _seekPending = true;
            _bar?.SetPlaying(false);
        }

        /// <summary>帧号变化才推送视图（Push 为全量同步，避免每帧重复推送）。
        /// 日志投递：_seekPending=true（Seek/重播/切文件）= 清空+全量重投；否则增量追加。
        /// 连续推进（含 2x/4x 补帧）走增量路径，避免清空→重投的 TMP 渲染抖动导致视觉滞后。</summary>
        private void RenderCurrent()
        {
            if (_player == null || _synchronizer == null || _player.CurrentFrame == _lastRenderedFrame)
            {
                return;
            }

            _lastRenderedFrame = _player.CurrentFrame;
            _synchronizer.Push(_player.CurrentSnapshot);
            PushLogEntries();
            _bar?.SetFrames(_player.CurrentFrame, _player.TotalFrames);
            _bar?.SetPlaying(_player.State == ReplayPlayerState.Playing);
        }

        /// <summary>按帧把事件投递到日志面板（M15-T1-B1 操作日志）。
        /// _seekPending=true 时清空并从第 0 帧重投；否则从 _lastLoggedFrame 增量投递。
        /// 每帧调用 GetFrameEventRange 取该帧产出的事件——被拒命令产出 0 事件，
        /// 投递占位条目，保证帧号与日志条目一一对应（修复 AllEvents.Count 偏离帧号导致的固定滞后）。</summary>
        private void PushLogEntries()
        {
            if (_log == null || _player == null)
            {
                return;
            }

            if (_seekPending)
            {
                _log.Clear();
                _lastLoggedFrame = 0;
                _seekPending = false;
            }

            IReadOnlyList<GameEvent> allEvents = _player.AllEvents;
            for (int frame = _lastLoggedFrame; frame < _player.CurrentFrame; frame++)
            {
                (int start, int end) = _player.GetFrameEventRange(frame);
                if (end <= start)
                {
                    _log.AppendText("—— 第 " + (frame + 1) + " 步：操作被拒/无事件 ——");
                }
                else
                {
                    for (int i = start; i < end; i++)
                    {
                        _log.Append(allEvents[i]);
                    }
                }
            }
            _lastLoggedFrame = _player.CurrentFrame;
        }

        /// <summary>与 BattleSceneBootstrap.LoadTextResolver 同口径：读本地化 CSV，缺省回退 Key 显示。</summary>
        private static ITextResolver LoadTextResolver(string configDir)
        {
            string path = Path.Combine(configDir, LocalizationFile);
            try
            {
                if (!File.Exists(path))
                {
                    GameLog.Warn(LogChannel.Boot, "本地化表不存在，卡面将显示 Key：" + path);
                    return KeyPassthroughTextResolver.Instance;
                }

                return new CsvTextResolver(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                GameLog.Warn(LogChannel.Boot, "本地化表加载失败，卡面将显示 Key：" + e.Message);
                return KeyPassthroughTextResolver.Instance;
            }
        }
    }
}
