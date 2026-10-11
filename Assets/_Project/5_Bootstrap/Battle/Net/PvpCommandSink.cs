using Card.Core;
using Card.Domain.Match;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Input;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>
    /// 联机命令出口（M14-T3）：把表现层命令翻译为网络命令信封——
    /// 序列化 → 座位自增 seq → <see cref="PendingActionFeedback.NotifySubmitted"/> 在途登记 → 链路上行。
    /// 本地一律返回 <see cref="CommandResult.Valid()"/>（Docs/05 §6.1 基础版：
    /// 无本地规则校验，拒绝由权威 step 回执经 M14-T1 表现先行路径回退提示）。
    /// </summary>
    internal sealed class PvpCommandSink : ICommandSink
    {
        private readonly NetMatchDriver _driver;
        private readonly PendingActionFeedback _feedback;
        private int _nextSeq = 1;

        public PvpCommandSink(NetMatchDriver driver, PendingActionFeedback feedback)
        {
            _driver = Guard.NotNull(driver, nameof(driver));
            _feedback = Guard.NotNull(feedback, nameof(feedback));
        }

        public CommandResult Submit(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));

            int seq = _nextSeq++;
            JsonValue payload = JsonValue.Parse(CommandSerializer.Serialize(command));
            JsonValue envelope = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", payload)
                .Add("seq", JsonValue.From(seq));
            _driver.SendCommand(envelope.ToJson());
            _feedback.NotifySubmitted(seq, TryGetCardInstanceId(command));
            return CommandResult.Valid();
        }

        /// <summary>提取命令关联的卡实例 Id（在途待确认视觉挂在对应卡牌上；无关联为 null）。</summary>
        private static int? TryGetCardInstanceId(IGameCommand command)
        {
            if (command is PlayCardCommand play)
            {
                return play.CardInstanceId;
            }

            return command is AttackCommand attack ? attack.AttackerInstanceId : null;
        }
    }
}
