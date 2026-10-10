using System.Collections.Generic;
using System.Linq;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Agents
{
    public sealed partial class GreedyAiAgent
    {
        // ---------- 逐步模式入口 ----------

        /// <summary>
        /// 逐步模式入口：产出下一条命令（或跳过），返回 <c>true</c> = 还有步可跑、
        /// <c>false</c> = 已无可决策命令（guard exhausted 或 Done）。
        /// EndTurn 不由本方法产出——<see cref="OnTurnActivated"/> 同步模式下循环后自动发，
        /// 分帧模式下由外部（AiTurnRunner）在返回 <c>false</c> 后发。
        /// </summary>
        public bool StepOne()
        {
            if (_context == null || _guard.IsExhausted)
            {
                return false;
            }

            while (_phase != TurnPhase.Done)
            {
                if (!CanAct(_context.View))
                {
                    return false;
                }

                switch (_phase)
                {
                    case TurnPhase.HeroPower:
                        if (TryUseHeroPower())
                        {
                            return true;
                        }

                        _phase = TurnPhase.PlayCards;
                        continue;

                    case TurnPhase.PlayCards:
                        if (TryPlayOneCard())
                        {
                            return true;
                        }

                        _phase = TurnPhase.Attack;
                        continue;

                    case TurnPhase.Attack:
                        if (TryOneAttack())
                        {
                            return true;
                        }

                        _phase = TurnPhase.Done;
                        break;
                }
            }

            return false;
        }

        // ---------- 逐步模式私有辅助 ----------

        private bool TryUseHeroPower()
        {
            IReadOnlyPlayerState self = _context!.View.GetPlayer(PlayerId);
            if (self.Hero.PowerUsedThisTurn)
            {
                return false;
            }

            HeroPowerDefinition power = _database.RequireHeroPower(self.Hero.HeroPowerKey);
            if (!self.Mana.CanSpend(power.Cost))
            {
                return false;
            }

            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(power.Effects);
            if (!GreedyAiTargeting.TryChooseTarget(_context.View, PlayerId, power.TargetRule, effects, out TargetRef target))
            {
                return false;
            }

            _context.Submit(new UseHeroPowerCommand(PlayerId, target));
            _guard.RegisterStep(BuildStateSignature(_context.View));
            return true;
        }

        private bool TryPlayOneCard()
        {
            if (!TryPickPlayableCard(out int cardInstanceId, out TargetRef target))
            {
                return false;
            }

            CommandResult result = _context!.Submit(new PlayCardCommand(PlayerId, cardInstanceId, target));
            _guard.RegisterStep(BuildStateSignature(_context.View));
            if (result.IsInvalid)
            {
                // 预校验与引擎口径不一致：本回合内不再重试该候选（FR-6.5 轻量终止保障）。
                _rejectedCards.Add(cardInstanceId);
            }

            return true;
        }

        private bool TryPickPlayableCard(out int cardInstanceId, out TargetRef target)
        {
            cardInstanceId = 0;
            target = TargetRef.None;
            IReadOnlyPlayerState self = _context!.View.GetPlayer(PlayerId);
            bool boardFull = self.Board.Count >= (self.Board.Capacity ?? int.MaxValue);

            var candidates = self.Hand.Cards
                .Where(c => !_rejectedCards.Contains(c.InstanceId))
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
                if (GreedyAiTargeting.TryChooseTarget(
                    _context.View, PlayerId, candidate.Def.TargetRule, effects, out target))
                {
                    cardInstanceId = candidate.Card.InstanceId;
                    return true;
                }
            }

            return false;
        }

        private bool TryOneAttack()
        {
            IReadOnlyCardInstance? attacker = _context!.View.GetPlayer(PlayerId).Board.Cards
                .Where(c => CanAttack(c))
                .OrderBy(c => c.InstanceId)
                .FirstOrDefault();
            if (attacker == null)
            {
                return false;
            }

            if (!GreedyAiTargeting.TryChooseAttackTarget(_context.View, PlayerId, attacker, out TargetRef target))
            {
                return false;
            }

            CommandResult result = _context.Submit(new AttackCommand(PlayerId, attacker.InstanceId, target));
            _guard.RegisterStep(BuildStateSignature(_context.View));
            if (result.IsInvalid)
            {
                _exhaustedAttackers.Add(attacker.InstanceId);
                return true;  // 已尝试一次，不算"无动作"
            }

            _attacksUsed.TryGetValue(attacker.InstanceId, out int used);
            _attacksUsed[attacker.InstanceId] = used + 1;
            return true;
        }

        private bool CanAttack(IReadOnlyCardInstance card)
        {
            if (card.Attack <= 0 || _exhaustedAttackers.Contains(card.InstanceId))
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

            _attacksUsed.TryGetValue(card.InstanceId, out int used);
            int limit = (card.KeywordFlags & Keyword.Windfury) != 0 ? 2 : 1;
            return used < limit;
        }
    }
}
