using System.Globalization;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// MatchRecord JSON 序列化（M14-T5）：纯 BCL（Card.Core.JsonValue），不使用 JsonUtility。
    /// 格式：扁平对象 + outcome 对象 + steps 数组。
    /// </summary>
    public static class MatchRecordSerializer
    {
        public static string Serialize(MatchRecord record)
        {
            return ToJsonValue(record).ToJson();
        }

        public static JsonValue ToJsonValue(MatchRecord record)
        {
            Guard.NotNull(record, nameof(record));

            JsonValue root = JsonValue.Object()
                .Add("matchId", JsonValue.From(record.Outcome.MatchId))
                .Add("result", JsonValue.From(record.Outcome.Result.ToString()))
                .Add("winnerId", record.Outcome.WinnerId.HasValue
                    ? JsonValue.From(record.Outcome.WinnerId.Value)
                    : JsonValue.Null())
                .Add("turnNumber", JsonValue.From(record.Outcome.TurnNumber))
                .Add("reason", JsonValue.From(record.Outcome.Reason))
                .Add("startTick", JsonValue.From(record.Outcome.StartTick))
                .Add("endTick", JsonValue.From(record.Outcome.EndTick))
                .Add("durationTicks", JsonValue.From(record.Outcome.DurationTicks))
                .Add("playerNames", JsonValue.Array(
                    JsonValue.From(record.PlayerNames[0]),
                    JsonValue.From(record.PlayerNames[1])))
                .Add("endReasonNet", record.EndReasonNet != null
                    ? JsonValue.From(record.EndReasonNet)
                    : JsonValue.Null());

            JsonValue[] stepItems = new JsonValue[record.Steps.Count];
            for (int i = 0; i < record.Steps.Count; i++)
            {
                MatchStepRecord s = record.Steps[i];
                stepItems[i] = JsonValue.Object()
                    .Add("sequence", JsonValue.From(s.Sequence))
                    .Add("commandType", JsonValue.From(s.CommandType))
                    .Add("playerId", JsonValue.From(s.PlayerId))
                    .Add("accepted", JsonValue.From(s.Accepted))
                    .Add("error", JsonValue.From(s.Error.ToString()))
                    .Add("phaseAfter", JsonValue.From(s.PhaseAfter.ToString()))
                    .Add("matchFinished", JsonValue.From(s.MatchFinished));
            }

            root.Add("steps", JsonValue.Array(stepItems));
            return root;
        }
    }
}
