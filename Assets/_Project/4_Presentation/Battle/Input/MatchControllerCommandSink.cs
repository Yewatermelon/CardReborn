using Card.Application.Match;
using Card.Core;
using Card.Domain.Match;

namespace Card.Presentation.Battle.Input
{
    /// <summary>
    /// <see cref="ICommandSink"/> 的生产适配器（M5-T4；M5-T8 起只依赖 <see cref="ICommandAuthority"/>）：
    /// 把命令透传给权威侧 <see cref="ICommandAuthority.Submit"/>，并原样返回
    /// <see cref="CommandResult"/>（含 <see cref="CommandError"/> 拒绝原因）。
    /// 类型层面不接触 <c>MatchController</c>，表现层拿不到可变状态。
    /// 场景装配（M6-T1）时与 <see cref="PlayerInputController.Initialize"/> 配合。
    /// </summary>
    public sealed class MatchControllerCommandSink : ICommandSink
    {
        private readonly ICommandAuthority _authority;

        public MatchControllerCommandSink(ICommandAuthority authority)
        {
            _authority = Guard.NotNull(authority, nameof(authority));
        }

        public CommandResult Submit(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));
            return _authority.Submit(command);
        }
    }
}
