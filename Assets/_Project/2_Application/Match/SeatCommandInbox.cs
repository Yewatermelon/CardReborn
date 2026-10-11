using System;

namespace Card.Application.Match
{
    /// <summary>
    /// 座位命令收件箱（M13-T3）：按座位独立维护"已消费的最大命令序号"与最近一条
    /// 回执原文，支撑重复包识别与幂等重放。口径：只对最近一条序号做幂等重放
    /// （TCP 保序下客户端未收到回执而重发的只会是最近一条），更早序号的重发一律
    /// 按过期丢弃。序号消费 = "处理过"（含被 RuleEngine 拒绝），不代表接受。
    /// </summary>
    public sealed class SeatCommandInbox
    {
        private string _lastPayloadJson = string.Empty;
        private string _lastStepJson = string.Empty;

        /// <summary>已消费的最大命令序号；0 表示尚未消费过任何带序号命令。</summary>
        public int LastSeq { get; private set; }

        /// <summary>最近消费命令的回执原文（幂等重放载体）。</summary>
        public string LastStepJson
        {
            get { return _lastStepJson; }
        }

        /// <summary>序号是否可执行（严格大于已消费最大序号；允许跳号）。</summary>
        public bool IsExecutable(int seq)
        {
            return seq > LastSeq;
        }

        /// <summary>载荷原文是否与最近消费命令一致（幂等重放条件）。</summary>
        public bool MatchesLastPayload(string payloadJson)
        {
            return string.Equals(payloadJson, _lastPayloadJson, StringComparison.Ordinal);
        }

        /// <summary>记录一次消费：序号推进 + 缓存载荷与回执原文。</summary>
        public void Record(int seq, string payloadJson, string stepJson)
        {
            LastSeq = seq;
            _lastPayloadJson = payloadJson;
            _lastStepJson = stepJson;
        }
    }
}
