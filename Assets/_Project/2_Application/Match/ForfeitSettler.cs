using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 判负结算器（M13-T8；Docs/05 §8.2）：把判负者英雄生命置 0，
    /// 终局判定仍由控制器在结算后统一调用 <see cref="MatchEvaluator"/>（入口唯一——
    /// 对手获胜、事件与终局状态全部走既有终局流水线）。结算器不写终局。
    /// </summary>
    public sealed class ForfeitSettler : ICommandSettler
    {
        public bool CanSettle(IGameCommand command)
        {
            return command is ForfeitCommand;
        }

        public void Settle(SettlementContext context, IGameCommand command)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(command, nameof(command));

            var forfeit = (ForfeitCommand)command;
            context.State.GetPlayer(forfeit.PlayerId).Hero.Health = 0;
        }
    }
}
