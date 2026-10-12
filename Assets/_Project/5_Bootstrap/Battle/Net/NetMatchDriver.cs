using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Match;
using Card.Network;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>
    /// 网络对局客户端驱动（M14-T3）：持有 <see cref="ClientStateSync"/>，消费
    /// <see cref="INetMatchLink"/> 下行（快照基线 / 权威增量），解析 step 事件为追加式
    /// 列表供表现层泵消费；跟踪终局（step.finished 与 MatchEnd 帧）。纯 BCL，无 Unity 依赖。
    /// 版本空洞（VersionGap，M13-T5）：自动上行 SnapshotRequest 并以收到的快照重置基线。
    /// </summary>
    internal sealed class NetMatchDriver
    {
        private readonly INetMatchLink _link;
        private readonly ClientStateSync _sync = new ClientStateSync();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private bool _hasSnapshot;
        private bool _finished;
        private int? _winner;
        private string? _endReason;

        /// <param name="link">座位传输适配。</param>
        /// <param name="localSeat">本地座位（房主 0 / 加入方 1，仅记录用）。</param>
        public NetMatchDriver(INetMatchLink link, int localSeat)
        {
            _link = Guard.NotNull(link, nameof(link));
            LocalSeat = localSeat;
            _link.MatchEnded += OnMatchEndFrame;
        }

        /// <summary>本地座位 Id。</summary>
        public int LocalSeat { get; }

        /// <summary>客户端状态同步器（快照基线 + 增量应用）。</summary>
        public ClientStateSync Sync => _sync;

        /// <summary>是否已收到开局快照（战斗装配前置条件）。</summary>
        public bool HasSnapshot => _hasSnapshot;

        /// <summary>链路是否已开局（房主=对手 Ready；加入方=收到 MatchStart）。</summary>
        public bool MatchStarted => _link.MatchStarted;

        /// <summary>链路是否存活。</summary>
        public bool LinkAlive => _link.Alive;

        /// <summary>对局是否已终局（step.finished / 快照状态 / MatchEnd 帧）。</summary>
        public bool IsFinished => _finished;

        /// <summary>终局胜者座位（MatchEndedEvent / MatchEnd 帧；null=作废或未知）。</summary>
        public int? WinnerId => _winner;

        /// <summary>终局原因（MatchEnd 帧优先，缺省回落 MatchEndedEvent 原因）。</summary>
        public string? EndReason => _endReason;

        /// <summary>解析后的权威事件（追加式；构造事件泵后继续增长即增量派发）。</summary>
        public IReadOnlyList<GameEvent> Events => _events;

        /// <summary>权威 step 裁决结果（含拒绝）——驱动 PendingActionFeedback。</summary>
        public event Action<StepOutcome>? StepApplied;

        /// <summary>房主侧：向权威发本地 hello，取座位 0 投影的开局快照。</summary>
        public void Start()
        {
            _link.SendUplink("{\"kind\":\"hello\"}");
        }

        /// <summary>上行一条命令信封（PvpCommandSink 用）。</summary>
        public void SendCommand(string envelopeJson)
        {
            _link.SendUplink(envelopeJson);
        }

        /// <summary>泵驱动：消费链路下行；返回本次应用的状态推进条数（快照 + 接受的增量）。</summary>
        public int Pump()
        {
            _appliedCount = 0;
            _link.Pump(ConsumeDownlink);
            return _appliedCount;
        }

        /// <summary>清空在途命令登记（快照重置恢复后调用）。</summary>
        public void ClearPending()
        {
            PendingCleared?.Invoke();
        }

        /// <summary>快照重置恢复后通知表现层清空在途视觉（M14-T1 契约）。</summary>
        public event Action? PendingCleared;

        public void Dispose()
        {
            _link.Dispose();
        }

        // -----------------------------------------------------------------
        // 私有：下行消费
        // -----------------------------------------------------------------

        private int _appliedCount;

        private void ConsumeDownlink(JsonValue message)
        {
            if (!message.TryGetMember("kind", out JsonValue kind) || kind.Kind != JsonKind.String)
            {
                NetworkLogger.Warn("NetMatchDriver 丢弃缺少 kind 的下行消息。");
                return;
            }

            if (kind.StringValue == "snapshot")
            {
                ApplySnapshot(message);
                return;
            }

            if (kind.StringValue == "step")
            {
                ApplyStep(message);
                return;
            }

            NetworkLogger.Warn("NetMatchDriver 忽略未知下行 kind：\"" + kind.StringValue + "\"。");
        }

        private void ApplySnapshot(JsonValue message)
        {
            _sync.ApplySnapshot(message);
            _hasSnapshot = true;
            _finished |= _sync.State.IsFinished;
            _appliedCount++;
            PendingCleared?.Invoke(); // 基线重置（开局或 VersionGap 恢复）→ 表现层清在途
            GameLog.Info(LogChannel.Network, "NetMatchDriver 收到快照 version=" + _sync.LocalVersion
                + " activePlayer=" + _sync.State.ActivePlayerId + " turn=" + _sync.State.TurnNumber);
        }

        private void ApplyStep(JsonValue message)
        {
            StepOutcome outcome = _sync.ApplyStep(message);

            if (outcome.Status == StepApplyStatus.Applied)
            {
                AppendEvents(outcome.Events);
                _appliedCount++;
                GameLog.Info(LogChannel.Network, "NetMatchDriver 应用 step version=" + _sync.LocalVersion
                    + " activePlayer=" + _sync.State.ActivePlayerId + " turn=" + _sync.State.TurnNumber
                    + " accepted=" + outcome.Accepted);
            }
            else if (outcome.Status == StepApplyStatus.VersionGap)
            {
                // 版本空洞（M13-T5）：请求按座位裁剪的当前版本快照重置基线
                GameLog.Warn(LogChannel.Network, "NetMatchDriver 版本空洞：local=" + _sync.LocalVersion + "，请求快照恢复。");
                _link.SendUplink(ClientStateSync.BuildSnapshotRequest(_sync.LocalVersion));
            }
            else
            {
                GameLog.Info(LogChannel.Network, "NetMatchDriver 丢弃 step status=" + outcome.Status);
            }

            TrackFinished(message);
            StepApplied?.Invoke(outcome);
        }

        /// <summary>追加 step 事件并扫描终局事件（胜者 / 原因）。</summary>
        private void AppendEvents(IReadOnlyList<JsonValue> eventJsons)
        {
            for (int i = 0; i < eventJsons.Count; i++)
            {
                GameEvent gameEvent = EventSerializer.ReadEvent(eventJsons[i]);
                _events.Add(gameEvent);
                if (gameEvent is MatchEndedEvent ended)
                {
                    _winner ??= ended.WinnerId;
                    _endReason ??= ended.Reason;
                }
            }
        }

        private void TrackFinished(JsonValue step)
        {
            if (step.TryGetMember("finished", out JsonValue finished) && finished.Kind == JsonKind.Bool)
            {
                _finished |= finished.BoolValue;
            }
        }

        private void OnMatchEndFrame(int? winner, string reason)
        {
            _finished = true;
            _winner ??= winner;
            _endReason = string.IsNullOrEmpty(reason) ? _endReason : reason;
        }
    }
}
