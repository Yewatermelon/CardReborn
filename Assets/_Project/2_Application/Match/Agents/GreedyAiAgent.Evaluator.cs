using System.Collections.Generic;
using System.Linq;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// GreedyAiAgent Hard 难度评估与斩杀意识（M7-T5）。
    /// 出牌评估：费用+场控+效果多维打分（比 Greedy 纯费用排序更智能）。
    /// 斩杀意识：无嘲讽且总攻击力 ≥ 对手血量+护甲时跳过解场直接打脸。
    /// </summary>
    public sealed partial class GreedyAiAgent
    {
        /// <summary>
        /// Hard 难度出牌候选评估：与 Normal 的"高费优先"基础一致，
        /// 但对效果牌（伤害/消灭/过牌/跳费）额外加权，让 Hard 在合适场景下
        /// 比 Greedy 更聪明——例如有可斩杀目标时优先出伤害法术。
        ///
        /// 费用权重中等保证大方向与 Greedy 趋同，效果权重让"费用相近"时法术反超随从。
        /// </summary>
        private int EvaluatePlayCandidate(IReadOnlyCardInstance card, CardDefinition def)
        {
            // 费用权重中等：保证大方向与 Greedy 趋同，但效果牌在"费用相近"时能反超随从
            int score = def.Cost * 5;

            if (def.Type == CardType.Minion)
            {
                score += (def.Attack + def.Health) * 2;
                if (card.KeywordFlags.HasFlag(Keyword.Charge)) score += 8;
            }

            // 法术效果加权：伤害/消灭/过牌
            IReadOnlyList<TriggeredEffect> effects = def.Effects.Count > 0
                ? EffectParser.Parse(def.Effects)
                : NoEffects;
            foreach (TriggeredEffect triggered in effects)
            {
                if (triggered.Trigger != Trigger.OnPlay) continue;
                switch (triggered.Effect)
                {
                    case DamageEffectData d: score += d.Amount * 4; break;
                    case DestroyEffectData _: score += 40; break;
                    case DrawCardEffectData dr: score += dr.Count * 6; break;
                    case GainManaEffectData m: score += m.Amount * 3; break;
                    case GainArmorEffectData a: score += a.Amount * 1; break;
                }
            }

            return score;
        }

        /// <summary>Hard 斩杀检查：无嘲讽 + 对手血量+护甲 ≤ 我方可攻击者攻击力之和。</summary>
        private bool CanKillEnemy()
        {
            IReadOnlyPlayerState self = _context!.View.GetPlayer(PlayerId);
            IReadOnlyPlayerState enemy = _context!.View.GetPlayer(FindEnemyId());

            // 有嘲讽时不能跳过解场（规则强制）
            if (enemy.Board.Cards.Any(c => (c.KeywordFlags & Keyword.Taunt) != 0))
            {
                return false;
            }

            int totalAtk = self.Board.Cards
                .Where(c => CanAttack(c))
                .Sum(c => c.Attack);
            return totalAtk > 0 && totalAtk >= enemy.Hero.Health + enemy.Hero.Armor;
        }

        private int FindEnemyId()
        {
            foreach (IReadOnlyPlayerState p in _context!.View.Players)
            {
                if (p.Id != PlayerId) return p.Id;
            }

            return PlayerId == 0 ? 1 : 0;
        }
    }
}
