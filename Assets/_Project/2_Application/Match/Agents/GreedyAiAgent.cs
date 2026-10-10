using System.Collections.Generic;
using System.Linq;
using System.Text;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// 贪心基础 AI（M7-T2，FR-6.2）：激活回调内同步跑完一整个回合——
    /// 英雄技能（一次）→ 出牌循环（高费优先）→ 攻击循环（先解场后打脸）→ 结束回合。
    /// 决策只读 <see cref="IAgentContext.View"/> + <see cref="CardDatabase"/>（数值/规则来自配置，铁律 6）；
    /// 命令一律 <see cref="IAgentContext.Submit"/> 上行经 RuleEngine 校验（铁律 5、FR-6.5）。
    /// 无随机：同种子命令序列逐位一致；被拒候选本回合不重试；候选耗尽必发 EndTurn（终止性）。
    /// 回合终止保障（M7-T3，FR-6.4）：决策循环接入 <see cref="TurnGuard"/>
    /// （可配置步数上限 + IClock 时间上限 + 无进展检测），任一触发即停止决策、必发 EndTurn。
    /// </summary>
    public sealed class GreedyAiAgent : IPlayerAgent
    {
        private static readonly IReadOnlyList<TriggeredEffect> NoEffects = new TriggeredEffect[0];

        private readonly CardDatabase _database;
        private readonly TurnGuard _guard;

        public GreedyAiAgent(
            int playerId, CardDatabase database,
            TurnGuardOptions? guardOptions = null, IClock? clock = null)
        {
            PlayerId = playerId;
            _database = Guard.NotNull(database, nameof(database));
            _guard = new TurnGuard(guardOptions, clock);
        }

        public int PlayerId { get; }

        public void OnTurnActivated(IAgentContext context)
        {
            Guard.NotNull(context, nameof(context));
            _guard.OnActivationStarted();
            HashSet<int> rejectedCards = new HashSet<int>();
            HashSet<int> exhaustedAttackers = new HashSet<int>();
            Dictionary<int, int> attacksUsed = new Dictionary<int, int>();

            TryUseHeroPower(context);
            PlayCards(context, rejectedCards);
            AttackWithAll(context, exhaustedAttackers, attacksUsed);

            if (CanAct(context.View))
            {
                // EndTurn 不受守卫约束：守卫只停止"后续决策"，回合必须收尾（FR-6.4）。
                context.Submit(new EndTurnCommand(PlayerId));
            }
        }

        public void OnTurnDeactivated()
        {
            // 无跨回合状态，空操作（幂等）。
        }

        private bool CanAct(IReadOnlyMatchState view)
        {
            return !view.IsFinished && view.ActivePlayerId == PlayerId;
        }

        private void TryUseHeroPower(IAgentContext context)
        {
            if (!CanAct(context.View) || _guard.IsExhausted)
            {
                return;
            }

            IReadOnlyPlayerState self = context.View.GetPlayer(PlayerId);
            if (self.Hero.PowerUsedThisTurn)
            {
                return;
            }

            HeroPowerDefinition power = _database.RequireHeroPower(self.Hero.HeroPowerKey);
            if (!self.Mana.CanSpend(power.Cost))
            {
                return;
            }

            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(power.Effects);
            if (!GreedyAiTargeting.TryChooseTarget(context.View, PlayerId, power.TargetRule, effects, out TargetRef target))
            {
                return;
            }

            context.Submit(new UseHeroPowerCommand(PlayerId, target));
            _guard.RegisterStep(BuildStateSignature(context.View));
        }

        private void PlayCards(IAgentContext context, HashSet<int> rejectedCards)
        {
            while (CanAct(context.View) && !_guard.IsExhausted)
            {
                if (!TryPickPlayableCard(context.View, rejectedCards, out int cardInstanceId, out TargetRef target))
                {
                    return;
                }

                CommandResult result = context.Submit(new PlayCardCommand(PlayerId, cardInstanceId, target));
                _guard.RegisterStep(BuildStateSignature(context.View));
                if (result.IsInvalid)
                {
                    // 预校验与引擎口径不一致：本回合内不再重试该候选（FR-6.5 轻量终止保障）。
                    rejectedCards.Add(cardInstanceId);
                }
            }
        }

        private bool TryPickPlayableCard(
            IReadOnlyMatchState view, HashSet<int> rejectedCards,
            out int cardInstanceId, out TargetRef target)
        {
            cardInstanceId = 0;
            target = TargetRef.None;
            IReadOnlyPlayerState self = view.GetPlayer(PlayerId);
            bool boardFull = self.Board.Count >= (self.Board.Capacity ?? int.MaxValue);

            var candidates = self.Hand.Cards
                .Where(c => !rejectedCards.Contains(c.InstanceId))
                .Select(c => new { Card = c, Def = _database.RequireCard(c.CardKey) })
                .Where(x => self.Mana.CanSpend(x.Def.Cost))
                .Where(x => x.Def.Type != CardType.Minion || !boardFull)
                .OrderByDescending(x => x.Def.Cost)
                .ThenBy(x => x.Card.InstanceId);

            foreach (var candidate in candidates)
            {
                IReadOnlyList<TriggeredEffect> effects = candidate.Def.Effects.Count > 0
                    ? EffectParser.Parse(candidate.Def.Effects)
                    : NoEffects;
                if (GreedyAiTargeting.TryChooseTarget(view, PlayerId, candidate.Def.TargetRule, effects, out target))
                {
                    cardInstanceId = candidate.Card.InstanceId;
                    return true;
                }
            }

            return false;
        }

        private void AttackWithAll(
            IAgentContext context, HashSet<int> exhaustedAttackers,
            Dictionary<int, int> attacksUsed)
        {
            while (CanAct(context.View) && !_guard.IsExhausted)
            {
                IReadOnlyCardInstance? attacker = context.View.GetPlayer(PlayerId).Board.Cards
                    .Where(c => CanAttack(c, exhaustedAttackers, attacksUsed))
                    .OrderBy(c => c.InstanceId)
                    .FirstOrDefault();
                if (attacker == null)
                {
                    return;
                }

                if (!GreedyAiTargeting.TryChooseAttackTarget(context.View, PlayerId, attacker, out TargetRef target))
                {
                    return;
                }

                CommandResult result = context.Submit(new AttackCommand(PlayerId, attacker.InstanceId, target));
                _guard.RegisterStep(BuildStateSignature(context.View));
                if (result.IsInvalid)
                {
                    exhaustedAttackers.Add(attacker.InstanceId);
                    continue;
                }

                attacksUsed.TryGetValue(attacker.InstanceId, out int used);
                attacksUsed[attacker.InstanceId] = used + 1;
            }
        }

        /// <summary>
        /// 决策相关局面签名（确定性遍历、无 Random）：双方英雄/法力/分区张数/疲劳 + 场面每张卡关键值。
        /// 签名不变 ≈ 命令对局面无实质影响（含被拒/合法但无效）——无进展检测的输入面。
        /// </summary>
        private static string BuildStateSignature(IReadOnlyMatchState view)
        {
            var sb = new StringBuilder();
            sb.Append('T').Append(view.TurnNumber)
                .Append(";P").Append(view.ActivePlayerId)
                .Append(";F").Append(view.IsFinished ? 1 : 0);
            foreach (IReadOnlyPlayerState player in view.Players)
            {
                sb.Append("|p").Append(player.Id)
                    .Append(",hp").Append(player.Hero.Health)
                    .Append(",ar").Append(player.Hero.Armor)
                    .Append(",pw").Append(player.Hero.PowerUsedThisTurn ? 1 : 0)
                    .Append(",m").Append(player.Mana.Current).Append('/').Append(player.Mana.Max)
                    .Append(",d").Append(player.Deck.Count)
                    .Append(",h").Append(player.Hand.Count)
                    .Append(",g").Append(player.Graveyard.Count)
                    .Append(",f").Append(player.FatigueCounter);
                foreach (IReadOnlyCardInstance card in player.Board.Cards)
                {
                    sb.Append(",b").Append(card.InstanceId)
                        .Append(':').Append(card.Attack).Append('/').Append(card.Health)
                        .Append(':').Append((int)card.StatusFlags)
                        .Append(':').Append((int)card.KeywordFlags);
                }
            }

            return sb.ToString();
        }

        private static bool CanAttack(
            IReadOnlyCardInstance card, HashSet<int> exhaustedAttackers, Dictionary<int, int> attacksUsed)
        {
            if (card.Attack <= 0 || exhaustedAttackers.Contains(card.InstanceId))
            {
                return false;
            }

            if ((card.StatusFlags & StatusFlags.Frozen) != 0)
            {
                return false;
            }

            bool sick = (card.StatusFlags & StatusFlags.SummoningSickness) != 0;
            bool ignoresSickness = (card.KeywordFlags & (Keyword.Charge | Keyword.Rush)) != 0;
            if (sick && !ignoresSickness)
            {
                return false;
            }

            attacksUsed.TryGetValue(card.InstanceId, out int used);
            int limit = (card.KeywordFlags & Keyword.Windfury) != 0 ? 2 : 1;
            return used < limit;
        }
    }
}
