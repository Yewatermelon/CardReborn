using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.PlayMode
{
    /// <summary>
    /// 冒烟级对局驱动器（M6-T2）：读只读视图，尝试出牌/攻击/结束回合，
    /// 靠 RuleEngine 接受/拒绝推进——不判规则、不复制规则逻辑。
    /// 驱动器只做"能出就出、能打就打"，不做最优选牌/选目标（M7 范围）。
    /// </summary>
    internal static class BattleSmokeDriver
    {
        /// <summary>
        /// 驱动对局直至终局或达到步数上限。每次 Submit（含被拒）计 1 步。
        /// </summary>
        internal static DriveResult Drive(
            MatchController controller,
            CardDatabase database,
            int maxSteps)
        {
            int steps = 0, plays = 0, attacks = 0, turns = 0;

            while (steps < maxSteps && !controller.IsFinished)
            {
                IReadOnlyMatchState view = controller.View;
                int activeId = view.ActivePlayerId;
                int enemyId = 1 - activeId;
                IReadOnlyPlayerState active = view.GetPlayer(activeId);
                IReadOnlyPlayerState enemy = view.GetPlayer(enemyId);

                if (TryPlayCard(controller, database, active, enemy, activeId, enemyId, ref steps, maxSteps))
                {
                    plays++;
                    continue;
                }

                if (TryAttack(controller, active, enemy, activeId, enemyId, ref steps, maxSteps))
                {
                    attacks++;
                    continue;
                }

                if (steps < maxSteps && !controller.IsFinished)
                {
                    steps++;
                    if (controller.Submit(new EndTurnCommand(activeId)).IsValid)
                    {
                        turns++;
                    }
                }
            }

            return new DriveResult(steps, plays, attacks, turns);
        }

        /// <summary>
        /// 尝试从手牌出一张牌：先试无目标，再试敌方英雄/随从，最后试己方随从。
        /// 每个 Submit 计入 steps；被拒不改状态，安全继续。
        /// </summary>
        private static bool TryPlayCard(
            MatchController controller,
            CardDatabase database,
            IReadOnlyPlayerState active,
            IReadOnlyPlayerState enemy,
            int activeId,
            int enemyId,
            ref int steps,
            int maxSteps)
        {
            // 物化快照——Submit 改底层状态，不能边遍历边改
            List<IReadOnlyCardInstance> handSnapshot = active.Hand.Cards.ToList();
            List<IReadOnlyCardInstance> enemyMinions = enemy.Board.Cards.ToList();
            List<IReadOnlyCardInstance> friendlyMinions = active.Board.Cards.ToList();

            foreach (IReadOnlyCardInstance card in handSnapshot)
            {
                if (steps >= maxSteps || controller.IsFinished)
                {
                    return false;
                }

                CardDefinition def = database.RequireCard(card.CardKey);
                if (def.Cost > active.Mana.Current)
                {
                    continue;
                }

                int id = card.InstanceId;

                // 无目标
                steps++;
                if (controller.Submit(new PlayCardCommand(activeId, id, TargetRef.None)).IsValid)
                {
                    return true;
                }

                if (steps >= maxSteps) return false;

                // 敌方英雄
                steps++;
                if (controller.Submit(new PlayCardCommand(activeId, id, TargetRef.ForHero(enemyId))).IsValid)
                {
                    return true;
                }

                // 敌方随从
                foreach (IReadOnlyCardInstance m in enemyMinions)
                {
                    if (steps >= maxSteps) return false;
                    steps++;
                    if (controller.Submit(new PlayCardCommand(activeId, id, TargetRef.ForMinion(m.InstanceId))).IsValid)
                    {
                        return true;
                    }
                }

                // 己方随从
                foreach (IReadOnlyCardInstance m in friendlyMinions)
                {
                    if (m.InstanceId == id) continue;
                    if (steps >= maxSteps) return false;
                    steps++;
                    if (controller.Submit(new PlayCardCommand(activeId, id, TargetRef.ForMinion(m.InstanceId))).IsValid)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试用场上随从攻击：先试敌方英雄，再试敌方随从（嘲讽优先由 RuleEngine 拦截）。
        /// </summary>
        private static bool TryAttack(
            MatchController controller,
            IReadOnlyPlayerState active,
            IReadOnlyPlayerState enemy,
            int activeId,
            int enemyId,
            ref int steps,
            int maxSteps)
        {
            List<IReadOnlyCardInstance> boardSnapshot = active.Board.Cards.ToList();
            List<IReadOnlyCardInstance> enemyMinions = enemy.Board.Cards.ToList();

            foreach (IReadOnlyCardInstance attacker in boardSnapshot)
            {
                if (steps >= maxSteps || controller.IsFinished)
                {
                    return false;
                }

                int id = attacker.InstanceId;

                // 敌方英雄
                steps++;
                if (controller.Submit(new AttackCommand(activeId, id, TargetRef.ForHero(enemyId))).IsValid)
                {
                    return true;
                }

                // 敌方随从
                foreach (IReadOnlyCardInstance target in enemyMinions)
                {
                    if (steps >= maxSteps) return false;
                    steps++;
                    if (controller.Submit(new AttackCommand(activeId, id, TargetRef.ForMinion(target.InstanceId))).IsValid)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>驱动结果统计。</summary>
    internal readonly struct DriveResult
    {
        public int StepsExecuted { get; }
        public int PlaysAccepted { get; }
        public int AttacksAccepted { get; }
        public int TurnsTaken { get; }

        public DriveResult(int stepsExecuted, int playsAccepted, int attacksAccepted, int turnsTaken)
        {
            StepsExecuted = stepsExecuted;
            PlaysAccepted = playsAccepted;
            AttacksAccepted = attacksAccepted;
            TurnsTaken = turnsTaken;
        }
    }
}
