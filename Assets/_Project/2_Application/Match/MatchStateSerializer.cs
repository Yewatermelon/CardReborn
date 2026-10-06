using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// MatchState JSON 序列化（M3-T9；FR-13.4，Docs/02 第 149 行）：
    /// 全量快照字符串往返，供权威宿主下发快照、重连与存盘。纯 BCL（Card.Core.JsonValue），不使用 JsonUtility。
    /// 写入/读取拆 partial：<see cref="MatchStateSerializer"/>（入口）、.Write、.Read。
    /// </summary>
    public static partial class MatchStateSerializer
    {
        private const int FormatVersion = 1;

        /// <summary>序列化为确定性 JSON 文本（字段顺序固定、2 空格缩进）。</summary>
        public static string Serialize(MatchState state)
        {
            Guard.NotNull(state, nameof(state));
            return WriteState(state).ToJson();
        }

        /// <summary>从 JSON 文本还原全新 MatchState；非法/版本不符抛异常，不返回半成品。</summary>
        public static MatchState Deserialize(string json)
        {
            Guard.NotNullOrWhiteSpace(json, nameof(json));
            return ReadState(JsonValue.Parse(json));
        }
    }
}
