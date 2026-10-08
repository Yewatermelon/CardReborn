using System;
using Card.Application.Match.Agents;
using Card.Presentation.Battle.Input;
using Card.Presentation.Battle.Targeting;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 人类玩家决策体（M7-T1）：把 <see cref="AgentMatchRunner"/> 的激活回调
    /// 翻译成输入层的"本地座位"——激活时把 PlayerInputController / TargetingController
    /// 的本地座位指向自己；命令仍由玩家点击 UI 后经 ICommandSink 上行，agent 不代下决策。
    /// 热座双人共用同一份输入组件，交棒屏（HotSeatHandler）仍是唯一输入门控，
    /// 回调赋值与其 SwitchPerspective 赋同值、幂等。
    /// </summary>
    internal sealed class HumanPlayerAgent : IPlayerAgent
    {
        private readonly PlayerInputController _input;
        private readonly TargetingController _targeting;

        public HumanPlayerAgent(int playerId, PlayerInputController input, TargetingController targeting)
        {
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "座位号必须非负。");
            PlayerId = playerId;
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        }

        public int PlayerId { get; }

        public void OnTurnActivated(IAgentContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            _input._localPlayerId = PlayerId;
            _targeting._localPlayerId = PlayerId;
        }

        public void OnTurnDeactivated()
        {
            // 交棒屏负责屏蔽非行动方输入；停活无需回收座位，下一位激活时会重新赋值。
        }
    }
}
