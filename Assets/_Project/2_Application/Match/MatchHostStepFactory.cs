using System;
using System.Collections.Generic;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 每座位 step 构造（M13-T6）：按座位投影 before/after 并 Diff，产出各自裁剪视角的 step JSON。
    /// 快照与增量统一经过 <see cref="PlayerViewProjector"/>（铁律 12：所有下发路径统一走裁剪）；
    /// 拒绝步 changes 为空、无泄露，仍按座位构建以复用同一出口。无状态纯函数。
    /// </summary>
    internal static class MatchHostStepFactory
    {
        /// <summary>为每个座位生成 step JSON（同座位多端点复用同一份）。</summary>
        public static Dictionary<int, string> BuildSeatSteps(
            MatchState before,
            MatchState after,
            IEnumerable<int> seats,
            ulong version,
            ulong basedOn,
            int? seq,
            bool accepted,
            CommandError error,
            string detail,
            bool finished,
            IReadOnlyList<GameEvent> events)
        {
            Dictionary<int, string> steps = new Dictionary<int, string>();
            foreach (int seat in seats)
            {
                IReadOnlyList<StateChange> changes = accepted
                    ? MatchStateDiffer.Diff(
                        PlayerViewProjector.Project(before, seat),
                        PlayerViewProjector.Project(after, seat))
                    : Array.Empty<StateChange>();

                steps[seat] = MatchHostProtocol
                    .WriteStep(version, basedOn, seq, accepted, error, detail, finished, changes, events)
                    .ToJson();
            }

            return steps;
        }
    }
}
