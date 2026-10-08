using System.Collections.Generic;
using System.Linq;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Domain.Match.Effects;

namespace Card.Application.Match.Agents
{
    /// <summary>
    /// GreedyAiAgent 的目标选择（M7-T2）：出牌/技能按 TargetRule + 效果取向选目标，
    /// 攻击按"嘲讽 → 有利交换 → 打脸"选目标。所有挑选按 InstanceId 升序 tiebreak，保证确定性。
    /// 潜行随从永不成为目标（引擎不管束，AI 自律）；攻击的嘲讽筛选对齐引擎、不看潜行。
    /// </summary>
    internal static class GreedyAiTargeting
    {
        /// <summary>Destroy 效果视为"必杀"，伤害估值取极大。</summary>
        private const int LethalDamage = int.MaxValue / 2;

        /// <summary>按目标规则为出牌/技能选目标；无合法目标时返回 false（调用方应放弃该候选）。</summary>
        public static bool TryChooseTarget(
            IReadOnlyMatchState view, int playerId, TargetRule rule,
            IReadOnlyList<TriggeredEffect> effects, out TargetRef target)
        {
            target = TargetRef.None;
            IReadOnlyPlayerState self = view.GetPlayer(playerId);
            IReadOnlyPlayerState enemy = FindEnemy(view, playerId);
            int damage = GetOnPlayDamage(effects);
            bool friendly = IsFriendlyEffect(effects);

            switch (rule)
            {
                case TargetRule.None:
                    return true;
                case TargetRule.Enemy:
                    target = PickOffensiveTarget(self, enemy, damage);
                    return true;
                case TargetRule.Any:
                    target = friendly ? PickFriendlyTarget(self) : PickOffensiveTarget(self, enemy, damage);
                    return true;
                case TargetRule.Friendly:
                    target = PickFriendlyTarget(self);
                    return true;
                case TargetRule.EnemyMinion:
                    return TryPickEnemyMinion(enemy, damage, out target);
                case TargetRule.FriendlyMinion:
                    return TryPickFriendlyMinion(self, out target);
                case TargetRule.AnyMinion:
                    return friendly
                        ? TryPickFriendlyMinion(self, out target)
                        : TryPickEnemyMinion(enemy, damage, out target);
                default:
                    return false;
            }
        }

        /// <summary>为一次攻击选目标：嘲讽（血最少）→ 有利交换（敌最高攻）→ 打脸。</summary>
        public static bool TryChooseAttackTarget(
            IReadOnlyMatchState view, int playerId, IReadOnlyCardInstance attacker, out TargetRef target)
        {
            IReadOnlyPlayerState enemy = FindEnemy(view, playerId);

            IReadOnlyCardInstance? taunt = enemy.Board.Cards
                .Where(c => (c.KeywordFlags & Keyword.Taunt) != 0)
                .OrderBy(c => c.Health)
                .ThenBy(c => c.InstanceId)
                .FirstOrDefault();
            if (taunt != null)
            {
                target = TargetRef.ForMinion(taunt.InstanceId);
                return true;
            }

            IReadOnlyCardInstance? trade = enemy.Board.Cards
                .Where(c => (c.KeywordFlags & Keyword.Stealth) == 0)
                .Where(c => IsFavorableTrade(attacker, c))
                .OrderByDescending(c => c.Attack)
                .ThenBy(c => c.InstanceId)
                .FirstOrDefault();
            if (trade != null)
            {
                target = TargetRef.ForMinion(trade.InstanceId);
                return true;
            }

            target = TargetRef.ForHero(enemy.Id);
            return true;
        }

        /// <summary>能杀且自身不死（或圣盾护体）、剧毒任意换，视为有利交换。</summary>
        private static bool IsFavorableTrade(IReadOnlyCardInstance attacker, IReadOnlyCardInstance defender)
        {
            if ((attacker.KeywordFlags & Keyword.Poisonous) != 0)
            {
                return true;
            }

            bool canKill = attacker.Attack >= defender.Health;
            bool survives = defender.Attack < attacker.Health
                || (attacker.StatusFlags & StatusFlags.DivineShield) != 0;
            return canKill && survives;
        }

        private static TargetRef PickOffensiveTarget(
            IReadOnlyPlayerState self, IReadOnlyPlayerState enemy, int damage)
        {
            if (damage > 0)
            {
                IReadOnlyCardInstance? killable = enemy.Board.Cards
                    .Where(c => (c.KeywordFlags & Keyword.Stealth) == 0)
                    .Where(c => damage >= LethalDamage || c.Health <= damage)
                    .OrderByDescending(c => c.Attack)
                    .ThenBy(c => c.InstanceId)
                    .FirstOrDefault();
                if (killable != null)
                {
                    return TargetRef.ForMinion(killable.InstanceId);
                }
            }

            return TargetRef.ForHero(enemy.Id);
        }

        private static TargetRef PickFriendlyTarget(IReadOnlyPlayerState self)
        {
            return TryPickFriendlyMinion(self, out TargetRef minion)
                ? minion
                : TargetRef.ForHero(self.Id);
        }

        private static bool TryPickEnemyMinion(IReadOnlyPlayerState enemy, int damage, out TargetRef target)
        {
            List<IReadOnlyCardInstance> candidates = enemy.Board.Cards
                .Where(c => (c.KeywordFlags & Keyword.Stealth) == 0)
                .ToList();
            if (candidates.Count == 0)
            {
                target = TargetRef.None;
                return false;
            }

            IOrderedEnumerable<IReadOnlyCardInstance> byAttackDesc = candidates
                .OrderByDescending(c => c.Attack)
                .ThenBy(c => c.InstanceId);
            IReadOnlyCardInstance? killable = damage > 0
                ? byAttackDesc.FirstOrDefault(c => damage >= LethalDamage || c.Health <= damage)
                : null;
            target = TargetRef.ForMinion((killable ?? byAttackDesc.First()).InstanceId);
            return true;
        }

        private static bool TryPickFriendlyMinion(IReadOnlyPlayerState self, out TargetRef target)
        {
            IReadOnlyCardInstance? best = self.Board.Cards
                .OrderByDescending(c => c.Attack)
                .ThenBy(c => c.InstanceId)
                .FirstOrDefault();
            if (best == null)
            {
                target = TargetRef.None;
                return false;
            }

            target = TargetRef.ForMinion(best.InstanceId);
            return true;
        }

        /// <summary>OnPlay 伤害估值：Damage 累加；Destroy 视为必杀；Composite 解包一层。无伤害返回 0。</summary>
        private static int GetOnPlayDamage(IReadOnlyList<TriggeredEffect> effects)
        {
            int damage = 0;
            foreach (TriggeredEffect triggered in effects)
            {
                if (triggered.Trigger != Trigger.OnPlay)
                {
                    continue;
                }

                damage = AddEffectDamage(damage, triggered.Effect);
            }

            return damage;
        }

        private static int AddEffectDamage(int damage, IEffectData effect)
        {
            switch (effect)
            {
                case DamageEffectData d:
                    return damage == LethalDamage ? damage : damage + d.Amount;
                case DestroyEffectData _:
                    return LethalDamage;
                case CompositeEffectData composite:
                    foreach (IEffectData inner in composite.Effects)
                    {
                        damage = AddEffectDamage(damage, inner);
                    }

                    return damage;
                default:
                    return damage;
            }
        }

        /// <summary>增益/治疗/护甲/过牌/跳费类取向（打己方）；伤害/消灭类视为进攻取向。</summary>
        private static bool IsFriendlyEffect(IReadOnlyList<TriggeredEffect> effects)
        {
            bool friendly = false;
            foreach (TriggeredEffect triggered in effects)
            {
                if (triggered.Trigger != Trigger.OnPlay)
                {
                    continue;
                }

                if (ContainsOffensive(triggered.Effect))
                {
                    return false;
                }

                friendly = true;
            }

            return friendly;
        }

        private static bool ContainsOffensive(IEffectData effect)
        {
            switch (effect)
            {
                case DamageEffectData _:
                case DestroyEffectData _:
                    return true;
                case CompositeEffectData composite:
                    return composite.Effects.Any(ContainsOffensive);
                default:
                    return false;
            }
        }

        private static IReadOnlyPlayerState FindEnemy(IReadOnlyMatchState view, int playerId)
        {
            foreach (IReadOnlyPlayerState player in view.Players)
            {
                if (player.Id != playerId)
                {
                    return player;
                }
            }

            return view.GetPlayer(playerId);
        }
    }
}
