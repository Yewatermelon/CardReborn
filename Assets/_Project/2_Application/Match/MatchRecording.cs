using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Application.Match
{
    /// <summary>
    /// 对局录制（M4-T9 ★）：初始 Setup（双方英雄+卡组）+ 种子 + 命令流水（紧凑 JSON 字符串列表）。
    /// 可导出/导入；同 seed 同命令重放应得到与原局一致的状态与事件序列。
    /// 纯 BCL（Card.Core.JsonValue），不使用 JsonUtility。
    /// </summary>
    public sealed class MatchRecording
    {
        public MatchRecording(
            MatchSetupRequest seat0,
            MatchSetupRequest seat1,
            int seed,
            IReadOnlyList<string> commands)
        {
            Seat0 = Guard.NotNull(seat0, nameof(seat0));
            Seat1 = Guard.NotNull(seat1, nameof(seat1));
            Seed = seed;
            Commands = Guard.NotNull(commands, nameof(commands));
        }

        /// <summary>座位 0 的开局请求。</summary>
        public MatchSetupRequest Seat0 { get; }

        /// <summary>座位 1 的开局请求。</summary>
        public MatchSetupRequest Seat1 { get; }

        /// <summary>洗牌与掷先手的随机种子。</summary>
        public int Seed { get; }

        /// <summary>命令流水（每条为 CommandSerializer.Serialize 的 JSON 文本）。</summary>
        public IReadOnlyList<string> Commands { get; }

        /// <summary>序列化为确定性 JSON 文本。</summary>
        public string ToJson()
        {
            JsonValue root = JsonValue.Object();
            root.Add("seed", JsonValue.From(Seed));
            root.Add("seat0", SerializeSetup(Seat0));
            root.Add("seat1", SerializeSetup(Seat1));

            JsonValue[] cmdItems = new JsonValue[Commands.Count];
            for (int i = 0; i < Commands.Count; i++)
            {
                cmdItems[i] = JsonValue.From(Commands[i]);
            }
            root.Add("commands", JsonValue.Array(cmdItems));

            return root.ToJson();
        }

        /// <summary>从 JSON 文本还原；非法/缺字段抛 <see cref="ArgumentException"/>。</summary>
        public static MatchRecording FromJson(string json)
        {
            Guard.NotNullOrWhiteSpace(json, nameof(json));

            JsonValue root = JsonValue.Parse(json);
            int seed = RequireMember(root, "seed").IntValue;
            MatchSetupRequest seat0 = DeserializeSetup(RequireMember(root, "seat0"));
            MatchSetupRequest seat1 = DeserializeSetup(RequireMember(root, "seat1"));

            JsonValue commandsValue = RequireMember(root, "commands");
            List<string> commands = new List<string>(commandsValue.Items.Count);
            for (int i = 0; i < commandsValue.Items.Count; i++)
            {
                commands.Add(commandsValue.Items[i].StringValue);
            }

            return new MatchRecording(seat0, seat1, seed, commands);
        }

        private static JsonValue SerializeSetup(MatchSetupRequest request)
        {
            JsonValue obj = JsonValue.Object();
            obj.Add("heroKey", JsonValue.From(request.HeroKey));

            JsonValue[] cards = new JsonValue[request.DeckCardKeys.Count];
            for (int i = 0; i < request.DeckCardKeys.Count; i++)
            {
                cards[i] = JsonValue.From(request.DeckCardKeys[i]);
            }
            obj.Add("deckCardKeys", JsonValue.Array(cards));

            return obj;
        }

        private static MatchSetupRequest DeserializeSetup(JsonValue value)
        {
            string heroKey = RequireMember(value, "heroKey").StringValue;

            JsonValue cardsValue = RequireMember(value, "deckCardKeys");
            List<string> cards = new List<string>(cardsValue.Items.Count);
            for (int i = 0; i < cardsValue.Items.Count; i++)
            {
                cards.Add(cardsValue.Items[i].StringValue);
            }

            return new MatchSetupRequest(heroKey, cards);
        }

        private static JsonValue RequireMember(JsonValue obj, string name)
        {
            if (!obj.TryGetMember(name, out JsonValue value))
            {
                throw new ArgumentException("录制 JSON 缺少成员：" + name);
            }

            return value;
        }
    }
}
