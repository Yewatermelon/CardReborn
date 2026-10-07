using System;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// GameCommand 紧凑 JSON 序列化（M4-T9；FR-13/14 联网前置）。
    /// 纯 BCL（Card.Core.JsonValue），不使用 JsonUtility。
    /// 格式：{ "type": "PlayCard|Attack|UseHeroPower|EndTurn", "playerId": N, ... }
    /// </summary>
    public static class CommandSerializer
    {
        public static string Serialize(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));

            JsonValue obj = JsonValue.Object();
            obj.Add("playerId", JsonValue.From(command.PlayerId));

            switch (command)
            {
                case PlayCardCommand play:
                    obj.Add("type", JsonValue.From("PlayCard"));
                    obj.Add("cardInstanceId", JsonValue.From(play.CardInstanceId));
                    obj.Add("target", SerializeTarget(play.Target));
                    break;
                case AttackCommand attack:
                    obj.Add("type", JsonValue.From("Attack"));
                    obj.Add("attackerInstanceId", JsonValue.From(attack.AttackerInstanceId));
                    obj.Add("target", SerializeTarget(attack.Target));
                    break;
                case UseHeroPowerCommand power:
                    obj.Add("type", JsonValue.From("UseHeroPower"));
                    obj.Add("target", SerializeTarget(power.Target));
                    break;
                case EndTurnCommand _:
                    obj.Add("type", JsonValue.From("EndTurn"));
                    break;
                default:
                    throw new ArgumentException("未知命令类型：" + command.GetType().Name);
            }

            return obj.ToJson();
        }

        public static IGameCommand Deserialize(string json)
        {
            Guard.NotNullOrWhiteSpace(json, nameof(json));

            JsonValue root = JsonValue.Parse(json);
            string type = RequireMember(root, "type").StringValue;
            int playerId = RequireMember(root, "playerId").IntValue;

            switch (type)
            {
                case "PlayCard":
                    return new PlayCardCommand(
                        playerId,
                        RequireMember(root, "cardInstanceId").IntValue,
                        DeserializeTarget(RequireMember(root, "target")));
                case "Attack":
                    return new AttackCommand(
                        playerId,
                        RequireMember(root, "attackerInstanceId").IntValue,
                        DeserializeTarget(RequireMember(root, "target")));
                case "UseHeroPower":
                    return new UseHeroPowerCommand(
                        playerId,
                        DeserializeTarget(RequireMember(root, "target")));
                case "EndTurn":
                    return new EndTurnCommand(playerId);
                default:
                    throw new ArgumentException("未知命令类型标签：" + type);
            }
        }

        private static JsonValue RequireMember(JsonValue obj, string name)
        {
            if (!obj.TryGetMember(name, out JsonValue value))
            {
                throw new ArgumentException("命令 JSON 缺少成员：" + name);
            }

            return value;
        }

        private static JsonValue SerializeTarget(TargetRef target)
        {
            JsonValue obj = JsonValue.Object();
            obj.Add("kind", JsonValue.From(target.Kind.ToString()));
            obj.Add("targetId", JsonValue.From(target.TargetId));
            return obj;
        }

        private static TargetRef DeserializeTarget(JsonValue value)
        {
            string kind = RequireMember(value, "kind").StringValue;
            int targetId = RequireMember(value, "targetId").IntValue;

            switch (kind)
            {
                case "None":
                    return TargetRef.None;
                case "Minion":
                    return TargetRef.ForMinion(targetId);
                case "Hero":
                    return TargetRef.ForHero(targetId);
                default:
                    throw new ArgumentException("未知目标种类：" + kind);
            }
        }
    }
}
