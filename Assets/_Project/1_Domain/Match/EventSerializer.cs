using System;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// GameEvent 紧凑 JSON 序列化（M11-T6；FR-13.7 联网事件下发）。
    /// 纯 BCL（Card.Core.JsonValue），不使用 JsonUtility。
    /// 与 <see cref="CommandSerializer"/> 对称：type 标签 + switch 分支。
    /// 格式：{ "type": "PhaseChanged|TurnStarted|...", "sequence": N, ...各字段 }
    /// </summary>
    public static class EventSerializer
    {
        public static string Serialize(GameEvent e)
        {
            Guard.NotNull(e, nameof(e));
            return WriteEvent(e).ToJson();
        }

        public static JsonValue WriteEvent(GameEvent e)
        {
            JsonValue obj = JsonValue.Object()
                .Add("sequence", JsonValue.From(e.Sequence));

            switch (e)
            {
                case PhaseChangedEvent pc:
                    obj.Add("type", JsonValue.From("PhaseChanged"));
                    obj.Add("from", JsonValue.From(pc.From.ToString()));
                    obj.Add("to", JsonValue.From(pc.To.ToString()));
                    break;
                case TurnStartedEvent ts:
                    obj.Add("type", JsonValue.From("TurnStarted"));
                    obj.Add("turnNumber", JsonValue.From(ts.TurnNumber));
                    obj.Add("activeSeat", JsonValue.From(ts.ActiveSeat));
                    break;
                case TurnEndedEvent te:
                    obj.Add("type", JsonValue.From("TurnEnded"));
                    obj.Add("turnNumber", JsonValue.From(te.TurnNumber));
                    obj.Add("activeSeat", JsonValue.From(te.ActiveSeat));
                    break;
                case CardDrawnEvent cd:
                    obj.Add("type", JsonValue.From("CardDrawn"));
                    obj.Add("seat", JsonValue.From(cd.Seat));
                    obj.Add("cardInstanceId", JsonValue.From(cd.CardInstanceId));
                    break;
                case CardBurnedEvent cb:
                    obj.Add("type", JsonValue.From("CardBurned"));
                    obj.Add("seat", JsonValue.From(cb.Seat));
                    obj.Add("cardInstanceId", JsonValue.From(cb.CardInstanceId));
                    break;
                case FatigueEvent fe:
                    obj.Add("type", JsonValue.From("Fatigue"));
                    obj.Add("seat", JsonValue.From(fe.Seat));
                    obj.Add("damage", JsonValue.From(fe.Damage));
                    obj.Add("fatigueCounter", JsonValue.From(fe.FatigueCounter));
                    break;
                case DamageEvent de:
                    obj.Add("type", JsonValue.From("Damage"));
                    obj.Add("sourceInstanceId", WriteNullableInt(de.SourceInstanceId));
                    obj.Add("targetInstanceId", WriteNullableInt(de.TargetInstanceId));
                    obj.Add("targetHeroSeat", WriteNullableInt(de.TargetHeroSeat));
                    obj.Add("amount", JsonValue.From(de.Amount));
                    obj.Add("divineShieldConsumed", JsonValue.From(de.DivineShieldConsumed));
                    break;
                case HealingEvent he:
                    obj.Add("type", JsonValue.From("Healing"));
                    obj.Add("targetInstanceId", WriteNullableInt(he.TargetInstanceId));
                    obj.Add("targetHeroSeat", WriteNullableInt(he.TargetHeroSeat));
                    obj.Add("amount", JsonValue.From(he.Amount));
                    break;
                case CardDeathEvent cde:
                    obj.Add("type", JsonValue.From("CardDeath"));
                    obj.Add("cardInstanceId", JsonValue.From(cde.CardInstanceId));
                    break;
                case CardPlayedEvent cpe:
                    obj.Add("type", JsonValue.From("CardPlayed"));
                    obj.Add("seat", JsonValue.From(cpe.Seat));
                    obj.Add("cardInstanceId", JsonValue.From(cpe.CardInstanceId));
                    break;
                case AttackDeclaredEvent ade:
                    obj.Add("type", JsonValue.From("AttackDeclared"));
                    obj.Add("attackerInstanceId", JsonValue.From(ade.AttackerInstanceId));
                    obj.Add("targetInstanceId", WriteNullableInt(ade.TargetInstanceId));
                    obj.Add("targetHeroSeat", WriteNullableInt(ade.TargetHeroSeat));
                    break;
                case MatchEndedEvent me:
                    obj.Add("type", JsonValue.From("MatchEnded"));
                    obj.Add("result", JsonValue.From(me.Result.ToString()));
                    obj.Add("winnerId", WriteNullableInt(me.WinnerId));
                    obj.Add("turnNumber", JsonValue.From(me.TurnNumber));
                    obj.Add("reason", JsonValue.From(me.Reason));
                    break;
                default:
                    throw new ArgumentException("未知事件类型：" + e.GetType().Name);
            }

            return obj;
        }

        public static GameEvent Deserialize(string json)
        {
            Guard.NotNullOrWhiteSpace(json, nameof(json));
            return ReadEvent(JsonValue.Parse(json));
        }

        public static GameEvent ReadEvent(JsonValue root)
        {
            string type = RequireMember(root, "type").StringValue;
            int sequence = RequireMember(root, "sequence").IntValue;

            GameEvent e;
            switch (type)
            {
                case "PhaseChanged":
                    e = new PhaseChangedEvent(
        ReadEnum<TurnPhase>(root, "from"),
                        ReadEnum<TurnPhase>(root, "to"));
                    break;
                case "TurnStarted":
                    e = new TurnStartedEvent(
                        RequireMember(root, "turnNumber").IntValue,
                        RequireMember(root, "activeSeat").IntValue);
                    break;
                case "TurnEnded":
                    e = new TurnEndedEvent(
                        RequireMember(root, "turnNumber").IntValue,
                        RequireMember(root, "activeSeat").IntValue);
                    break;
                case "CardDrawn":
                    e = new CardDrawnEvent(
                        RequireMember(root, "seat").IntValue,
                        RequireMember(root, "cardInstanceId").IntValue);
                    break;
                case "CardBurned":
                    e = new CardBurnedEvent(
                        RequireMember(root, "seat").IntValue,
                        RequireMember(root, "cardInstanceId").IntValue);
                    break;
                case "Fatigue":
                    e = new FatigueEvent(
                        RequireMember(root, "seat").IntValue,
                        RequireMember(root, "damage").IntValue,
                        RequireMember(root, "fatigueCounter").IntValue);
                    break;
                case "Damage":
                    e = new DamageEvent(
                        ReadNullableInt(root, "sourceInstanceId"),
                        ReadNullableInt(root, "targetInstanceId"),
                        ReadNullableInt(root, "targetHeroSeat"),
                        RequireMember(root, "amount").IntValue,
                        RequireMember(root, "divineShieldConsumed").BoolValue);
                    break;
                case "Healing":
                    e = new HealingEvent(
                        ReadNullableInt(root, "targetInstanceId"),
                        ReadNullableInt(root, "targetHeroSeat"),
                        RequireMember(root, "amount").IntValue);
                    break;
                case "CardDeath":
                    e = new CardDeathEvent(RequireMember(root, "cardInstanceId").IntValue);
                    break;
                case "CardPlayed":
                    e = new CardPlayedEvent(
                        RequireMember(root, "seat").IntValue,
                        RequireMember(root, "cardInstanceId").IntValue);
                    break;
                case "AttackDeclared":
                    e = new AttackDeclaredEvent(
                        RequireMember(root, "attackerInstanceId").IntValue,
                        ReadNullableInt(root, "targetInstanceId"),
                        ReadNullableInt(root, "targetHeroSeat"));
                    break;
                case "MatchEnded":
                    e = new MatchEndedEvent(
                        ReadEnum<MatchResult>(root, "result"),
                        ReadNullableInt(root, "winnerId"),
                        RequireMember(root, "turnNumber").IntValue,
                        RequireMember(root, "reason").StringValue);
                    break;
                default:
                    throw new ArgumentException("未知事件类型标签：" + type);
            }

            e.Sequence = sequence;
            return e;
        }

        // -----------------------------------------------------------------
        // 辅助
        // -----------------------------------------------------------------

        private static JsonValue RequireMember(JsonValue obj, string name)
        {
            if (!obj.TryGetMember(name, out JsonValue value))
            {
                throw new ArgumentException("事件 JSON 缺少成员：" + name);
            }

            return value;
        }

        private static JsonValue WriteNullableInt(int? value)
        {
            return value.HasValue ? JsonValue.From(value.Value) : JsonValue.Null();
        }

        private static int? ReadNullableInt(JsonValue obj, string name)
        {
            if (!obj.TryGetMember(name, out JsonValue value))
            {
                throw new ArgumentException("事件 JSON 缺少成员：" + name);
            }

            return value.Kind == JsonKind.Null ? null : (int?)value.IntValue;
        }

        private static T ReadEnum<T>(JsonValue obj, string name) where T : struct, Enum
        {
            string raw = RequireMember(obj, name).StringValue;
            return (T)Enum.Parse(typeof(T), raw);
        }
    }
}
