using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>重放结果（M4-T9）：终局状态 + 事件序列 + 命令台账。</summary>
    public sealed class ReplayResult
    {
        public ReplayResult(
            MatchState finalState,
            IReadOnlyList<GameEvent> events,
            IReadOnlyList<MatchStepRecord> history)
        {
            FinalState = Guard.NotNull(finalState, nameof(finalState));
            Events = Guard.NotNull(events, nameof(events));
            History = Guard.NotNull(history, nameof(history));
        }

        public MatchState FinalState { get; }

        public IReadOnlyList<GameEvent> Events { get; }

        public IReadOnlyList<MatchStepRecord> History { get; }
    }

    /// <summary>
    /// 对局重放器（M4-T9 ★）：同 seed 重建初始状态 → 逐命令提交 → 收集结果。
    /// 供联网验证（上行命令流水可在权威侧重放）与离线复盘。
    /// </summary>
    public static class MatchReplayer
    {
        /// <summary>用录制中的 seed/卡组/命令重建整局；返回终局状态与事件。</summary>
        public static ReplayResult Replay(MatchRecording recording, CardDatabase database)
        {
            Guard.NotNull(recording, nameof(recording));
            Guard.NotNull(database, nameof(database));

            MatchState state = MatchFactory.Create(
                database,
                recording.Seat0,
                recording.Seat1,
                new SeededRandomProvider(recording.Seed));

            var controller = new MatchController(state, database);

            for (int i = 0; i < recording.Commands.Count; i++)
            {
                IGameCommand command = CommandSerializer.Deserialize(recording.Commands[i]);
                controller.Submit(command);
            }

            return new ReplayResult(controller.State, controller.Events, controller.History);
        }
    }
}
