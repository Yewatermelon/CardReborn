using Card.Application.Match;
using Card.Core;
using Card.Domain.Match;

namespace Card.Presentation.Battle.Input
{
    /// <summary>
    /// <see cref="ICommandSink"/> 的生产适配器（M5-T4）：把命令透传给
    /// <see cref="MatchController.Submit"/>，并原样返回权威侧的
    /// <see cref="CommandResult"/>（含 <see cref="CommandError"/> 拒绝原因）。
    /// 场景装配（M6-T1）时与 <see cref="PlayerInputController.Initialize"/> 配合。
    /// </summary>
    public sealed class MatchControllerCommandSink : ICommandSink
    {
        private readonly MatchController _controller;

        public MatchControllerCommandSink(MatchController controller)
        {
            _controller = Guard.NotNull(controller, nameof(controller));
        }

        public CommandResult Submit(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));
            return _controller.Submit(command);
        }
    }
}
