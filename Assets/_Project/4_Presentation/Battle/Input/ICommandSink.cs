using Card.Domain.Match;

namespace Card.Presentation.Battle.Input
{
    /// <summary>
    /// 命令出口抽象（M5-T4）：表现层把玩家意图翻译成的 <see cref="IGameCommand"/>
    /// 经本接口交给权威侧校验/结算；返回 <see cref="CommandResult"/> 供提示 UI 判读。
    /// 表现层经本接口与具体控制器（<c>MatchController</c> / 网络客户端）解耦。
    /// </summary>
    public interface ICommandSink
    {
        CommandResult Submit(IGameCommand command);
    }
}
