using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 命令权威入口的最小抽象（M5-T8 ★）：表现层只依赖本接口提交命令，
    /// 不持有 <see cref="MatchController"/>——从类型层面堵死表现层经控制器拿到
    /// 可变 <see cref="MatchState"/> 的路径（Docs/03 §5.6 表现层只读）。
    /// 本阶段由 <see cref="MatchController"/>（PVE 本地）实现；阶段二由
    /// 网络客户端实现同一接口（上行命令、下行状态）。
    /// </summary>
    public interface ICommandAuthority
    {
        /// <summary>校验并立即结算一条命令；非法命令被拒绝并携带错误码。</summary>
        CommandResult Submit(IGameCommand command);
    }
}
