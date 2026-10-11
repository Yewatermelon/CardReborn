using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 权威内部管理动作入口（M13-T8 partial；主文件 <see cref="MatchHost"/>）：
    /// <see cref="MatchHost.SubmitAuthority"/> 供宿主编排层代发权威决定——掉线判负
    /// （<see cref="ForfeitCommand"/>）与回合操作超时（<see cref="EndTurnCommand"/>）。
    /// 与客户端上行的差异：不走端点座位校验、不消费命令序号（权威自己的决定，
    /// 非客户端意图）；但走同一条"RuleEngine 校验 → 结算 → 每座位裁剪 step 下发 →
    /// version 推进"链路（铁律 5：任何状态变更都经权威校验）。
    /// </summary>
    public sealed partial class MatchHost
    {
        /// <summary>当前行动方座位（回合超时代发 EndTurn 用，M13-T8）。</summary>
        public int ActiveSeat => _controller.View.ActivePlayerId;

        /// <summary>终局结果；未终局为 null（MatchEnd 帧载荷来源，M13-T8）。</summary>
        public MatchOutcome? Outcome => _controller.LastOutcome;

        /// <summary>
        /// 提交权威内部管理动作：与 <see cref="MatchHost.ProcessCommand"/> 同链路结算，
        /// 但无座位归属/seq 裁决（inbox 与 seq 均为空——不记录回执）。返回命令结果。
        /// </summary>
        public CommandResult SubmitAuthority(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));
            return SubmitAndBroadcast(inbox: null, seq: null, command, JsonValue.Parse("{}"));
        }
    }
}
