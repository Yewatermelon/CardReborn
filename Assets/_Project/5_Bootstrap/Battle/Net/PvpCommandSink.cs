using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Input;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>
    /// 联机命令出口（M14-T3）：把表现层命令翻译为网络命令信封——
    /// 序列化 → 座位自增 seq → <see cref="PendingActionFeedback.NotifySubmitted"/> 在途登记 → 链路上行。
    /// 基础版本地不校验规则（Docs/05 §6.1，拒绝由权威 step 回执回退提示），但保留
    /// <b>目标流程预检</b>：命令需要目标而未带目标时返回 <see cref="CommandError.TargetRequired"/>
    /// （不消费 seq、不上行），使表现层指向流程正常开启——判据与 RuleEngine 同源
    /// （CardDefinition/HeroPowerDefinition.TargetRule），属 UI 流程判定而非规则裁决（铁律 4/12）。
    /// </summary>
    internal sealed class PvpCommandSink : ICommandSink
    {
        private readonly NetMatchDriver _driver;
        private readonly PendingActionFeedback _feedback;
        private readonly CardDatabase _database;
        private int _nextSeq = 1;

        public PvpCommandSink(NetMatchDriver driver, PendingActionFeedback feedback, CardDatabase database)
        {
            _driver = Guard.NotNull(driver, nameof(driver));
            _feedback = Guard.NotNull(feedback, nameof(feedback));
            _database = Guard.NotNull(database, nameof(database));
        }

        public CommandResult Submit(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));

            // 目标流程预检：需要目标的无目标命令交回表现层开指向（不消费 seq）
            if (Preflight(command) is CommandError required)
            {
                return CommandResult.Invalid(required, "该指令需要目标。");
            }

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

        /// <summary>预检需要目标的无目标命令；命中返回 TargetRequired，否则 null 放行。</summary>
        private CommandError? Preflight(IGameCommand command)
        {
            if (command is PlayCardCommand play && play.Target.IsNone
                && FindHandCard(play.PlayerId, play.CardInstanceId) is CardInstance card
                && _database.RequireCard(card.CardKey).TargetRule != TargetRule.None)
            {
                return CommandError.TargetRequired;
            }

            if (command is UseHeroPowerCommand power && power.Target.IsNone
                && FindPlayer(power.PlayerId) is PlayerState player
                && _database.RequireHeroPower(player.Hero.HeroPowerKey).TargetRule != TargetRule.None)
            {
                return CommandError.TargetRequired;
            }

            return null;
        }

        private CardInstance? FindHandCard(int playerId, int cardInstanceId)
        {
            if (FindPlayer(playerId) is not PlayerState player)
            {
                return null;
            }

            for (int i = 0; i < player.Hand.Cards.Count; i++)
            {
                if (player.Hand.Cards[i].InstanceId == cardInstanceId)
                {
                    return player.Hand.Cards[i];
                }
            }

            return null;
        }

        private PlayerState? FindPlayer(int playerId)
        {
            MatchState state = _driver.Sync.State;
            foreach (PlayerState player in state.Players)
            {
                if (player.Id == playerId)
                {
                    return player;
                }
            }

            return null;
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
