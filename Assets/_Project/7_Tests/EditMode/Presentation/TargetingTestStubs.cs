using System.Collections.Generic;
using Card.Domain.Match;
using Card.Presentation.Battle.Input;
using Card.Presentation.Battle.Targeting;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>指向子系统测试共享 stub（M5-T5）：输入/命中/命令出口。</summary>
    internal sealed class StubInputSource : IInputSource
    {
        public Vector2 PointerScreenPosition { get; set; }
        public bool IsConfirmPressed { get; set; }
        public bool IsCancelPressed { get; set; }
    }

    internal sealed class StubTargetPicker : ITargetPicker
    {
        public bool Hit;
        public TargetRef Picked;

        public bool TryPickTarget(Vector2 screenPosition, out TargetRef target)
        {
            target = Picked;
            return Hit;
        }
    }

    internal sealed class StubCommandSink : ICommandSink
    {
        public readonly List<IGameCommand> Received = new List<IGameCommand>();
        public CommandResult NextResult = CommandResult.Valid();

        public CommandResult Submit(IGameCommand command)
        {
            Received.Add(command);
            return NextResult;
        }
    }
}
