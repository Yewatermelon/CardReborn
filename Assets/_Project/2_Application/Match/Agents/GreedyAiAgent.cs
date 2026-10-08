using System.Collections.Generic;
using System.Linq;
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
    /// </summary>
    public sealed class GreedyAiAgent : IPlayerAgent
    {
        /// <summary>单次激活内提交数硬上限（防御性兜底，正式步数/时间上限归 M7-T3）。</summary>
        internal const int MaxSubmissionsPerActivation = 500;

        private static readonly IReadOnlyList<TriggeredEffect> NoEffects = new TriggeredEffect[0];

        private readonly CardDatabase _database;

        public GreedyAiAgent(int playerId, CardDatabase database)
        {
            PlayerId = playerId;
            _database = Guard.NotNull(database, nameof(database));
        }

        public int PlayerId { get; }

        public void OnTurnActivated(IAgentContext context)
        {
            Guard.NotNull(context, nameof(context));
            HashSet<int> rejectedCards = new HashSet<int>();
            HashSet<int> exhaustedAttackers = new HashSet<int>();
            Dictionary<int, int> attacksUsed = new Dictionary<int, int>();
            int submissions = 0;

            TryUseHeroPower(context, ref submissions);
            PlayCards(context, rejectedCards, ref submissions);
            AttackWithAll(context, exhaustedAttackers, attacksUsed, ref submissions);

            if (CanAct(context.View))
            {
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

        private void TryUseHeroPower(IAgentContext context, ref int submissions)
        {
            if (!CanAct(context.View) || submissions >= MaxSubmissionsPerActivation)
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
            submissions++;
        }

        private void PlayCards(IAgentContext context, HashSet<int> rejectedCards, ref int submissions)
        {
            while (CanAct(context.View) && submissions < MaxSubmissionsPerActivation)
            {
                if (!TryPickPlayableCard(context.View, rejectedCards, out int cardInstanceId, out TargetRef target))
                {
                    return;
                }

                CommandResult result = context.Submit(new PlayCardCommand(PlayerId, cardInstanceId, target));
                submissions++;
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
            Dictionary<int, int> attacksUsed, ref int submissions)
        {
            while (CanAct(context.View) && submissions < MaxSubmissionsPerActivation)
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
                submissions++;
                if (result.IsInvalid)
                {
                    exhaustedAttackers.Add(attacker.InstanceId);
                    continue;
                }

                attacksUsed.TryGetValue(attacker.InstanceId, out int used);
                attacksUsed[attacker.InstanceId] = used + 1;
            }
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
