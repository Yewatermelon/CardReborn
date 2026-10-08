using System;
using System.Collections.Generic;
using System.Linq;
using Card.Application.Match.Agents;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T1 测试桩：可注入"激活脚本"的 <see cref="IPlayerAgent"/>，
    /// 并记录激活/停活序列供断言。另提供 <see cref="GreedyTurn"/> 穷举脚本——
    /// 它<strong>不是</strong> AI 决策策略（出牌优先级/解场属 M7-T2），
    /// 只用于验证"算法形态的 agent 经同一接口驱动真实对局"：
    /// 像 M6-T2 冒烟驱动器一样只认权威侧 CommandResult，不复制任何规则。
    /// </summary>
    internal sealed class ScriptedPlayerAgent : IPlayerAgent
    {
        private readonly Action<IAgentContext>? _script;
        private readonly List<string>? _sharedLog;

        public ScriptedPlayerAgent(
            int playerId, Action<IAgentContext>? script = null, List<string>? sharedLog = null)
        {
            PlayerId = playerId;
            _script = script;
            _sharedLog = sharedLog;
        }

        public int PlayerId { get; }

        public int Activations { get; private set; }

        public int Deactivations { get; private set; }

        public IAgentContext? LastContext { get; private set; }

        /// <summary>激活/停活事件流："A0"/"D1" 形式，按回调顺序。</summary>
        public List<string> Events { get; } = new List<string>();

        public void OnTurnActivated(IAgentContext context)
        {
            Activations++;
            LastContext = context;
            Record("A");
            _script?.Invoke(context);
        }

        public void OnTurnDeactivated()
        {
            Deactivations++;
            Record("D");
        }

        private void Record(string prefix)
        {
            string item = prefix + PlayerId;
            Events.Add(item);
            _sharedLog?.Add(item);
        }

        /// <summary>最简脚本：以当前行动方身份结束回合。</summary>
        public static void EndTurnOnce(IAgentContext context)
        {
            context.Submit(new EndTurnCommand(context.ActivePlayerId));
        }

        /// <summary>
        /// 穷举脚本：在一次激活内不断尝试"技能→出牌→攻击"，
        /// 三者都无 accepted 后提交结束回合并返回（回合路由交回 runner）。
        /// 每次 accepted 命令按类型名计入 <paramref name="acceptedCounts"/>。
        /// </summary>
        public static void GreedyTurn(
            IAgentContext context,
            CardDatabase database,
            IDictionary<string, int> acceptedCounts,
            int maxSubmissions = 2000)
        {
            int seat = context.ActivePlayerId;
            int submissions = 0;
            while (!context.View.IsFinished
                && context.View.ActivePlayerId == seat
                && submissions < maxSubmissions)
            {
                if (TryHeroPower(context, seat, acceptedCounts, ref submissions))
                {
                    continue;
                }

                if (TryPlayCard(context, database, seat, acceptedCounts, ref submissions))
                {
                    continue;
                }

                if (TryAttack(context, seat, acceptedCounts, ref submissions))
                {
                    continue;
                }

                SubmitAndCount(context, new EndTurnCommand(seat), acceptedCounts, ref submissions);
                return;
            }
        }

        private static bool TryHeroPower(
            IAgentContext context, int seat, IDictionary<string, int> acceptedCounts, ref int submissions)
        {
            int enemyId = 1 - seat;
            IReadOnlyPlayerState enemy = context.View.GetPlayer(enemyId);

            // 无目标（可能合法）→ 敌英雄 → 敌随从；每个候选至多试一次。
            if (SubmitAndCount(context, new UseHeroPowerCommand(seat, TargetRef.None), acceptedCounts, ref submissions))
            {
                return true;
            }

            if (SubmitAndCount(context, new UseHeroPowerCommand(seat, TargetRef.ForHero(enemyId)), acceptedCounts, ref submissions))
            {
                return true;
            }

            foreach (IReadOnlyCardInstance minion in enemy.Board.Cards.ToList())
            {
                if (SubmitAndCount(context, new UseHeroPowerCommand(seat, TargetRef.ForMinion(minion.InstanceId)), acceptedCounts, ref submissions))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPlayCard(
            IAgentContext context, CardDatabase database, int seat,
            IDictionary<string, int> acceptedCounts, ref int submissions)
        {
            int enemyId = 1 - seat;
            IReadOnlyPlayerState self = context.View.GetPlayer(seat);
            IReadOnlyPlayerState enemy = context.View.GetPlayer(enemyId);

            // 物化快照：Submit 立即改底层状态，禁止边遍历边改。
            List<IReadOnlyCardInstance> hand = self.Hand.Cards.ToList();
            List<IReadOnlyCardInstance> enemyMinions = enemy.Board.Cards.ToList();
            List<IReadOnlyCardInstance> friendlyMinions = self.Board.Cards.ToList();

            foreach (IReadOnlyCardInstance card in hand)
            {
                CardDefinition definition = database.RequireCard(card.CardKey);
                if (definition.Cost > self.Mana.Current)
                {
                    continue;
                }

                int cardId = card.InstanceId;
                if (SubmitAndCount(context, new PlayCardCommand(seat, cardId, TargetRef.None), acceptedCounts, ref submissions)
                    || SubmitAndCount(context, new PlayCardCommand(seat, cardId, TargetRef.ForHero(enemyId)), acceptedCounts, ref submissions))
                {
                    return true;
                }

                foreach (IReadOnlyCardInstance minion in enemyMinions)
                {
                    if (SubmitAndCount(context, new PlayCardCommand(seat, cardId, TargetRef.ForMinion(minion.InstanceId)), acceptedCounts, ref submissions))
                    {
                        return true;
                    }
                }

                foreach (IReadOnlyCardInstance minion in friendlyMinions)
                {
                    if (minion.InstanceId == cardId)
                    {
                        continue;
                    }

                    if (SubmitAndCount(context, new PlayCardCommand(seat, cardId, TargetRef.ForMinion(minion.InstanceId)), acceptedCounts, ref submissions))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryAttack(
            IAgentContext context, int seat, IDictionary<string, int> acceptedCounts, ref int submissions)
        {
            int enemyId = 1 - seat;
            IReadOnlyPlayerState self = context.View.GetPlayer(seat);
            IReadOnlyPlayerState enemy = context.View.GetPlayer(enemyId);

            List<IReadOnlyCardInstance> board = self.Board.Cards.ToList();
            List<IReadOnlyCardInstance> enemyMinions = enemy.Board.Cards.ToList();

            foreach (IReadOnlyCardInstance attacker in board)
            {
                int attackerId = attacker.InstanceId;
                if (SubmitAndCount(context, new AttackCommand(seat, attackerId, TargetRef.ForHero(enemyId)), acceptedCounts, ref submissions))
                {
                    return true;
                }

                foreach (IReadOnlyCardInstance target in enemyMinions)
                {
                    if (SubmitAndCount(context, new AttackCommand(seat, attackerId, TargetRef.ForMinion(target.InstanceId)), acceptedCounts, ref submissions))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SubmitAndCount(
            IAgentContext context, IGameCommand command,
            IDictionary<string, int> acceptedCounts, ref int submissions)
        {
            submissions++;
            CommandResult result = context.Submit(command);
            if (!result.IsValid)
            {
                return false;
            }

            string name = command.GetType().Name;
            acceptedCounts.TryGetValue(name, out int count);
            acceptedCounts[name] = count + 1;
            return true;
        }
    }
}
