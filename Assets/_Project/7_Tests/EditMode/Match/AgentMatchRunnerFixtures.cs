using System;
using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T1 共享夹具：基于 <see cref="MatchControllerFixtures"/> 的真实配置/卡组/对局
    /// 构造 runner + 双 <see cref="ScriptedPlayerAgent"/>（疲劳脚本版与穷举脚本版）。
    /// </summary>
    internal static class AgentMatchRunnerFixtures
    {
        public static IPlayerAgent[] SeatAgents(
            Action<IAgentContext>? script0, Action<IAgentContext>? script1)
        {
            return new IPlayerAgent[]
            {
                new ScriptedPlayerAgent(0, script0),
                new ScriptedPlayerAgent(1, script1),
            };
        }

        public static (MatchController controller, AgentMatchRunner runner, ScriptedPlayerAgent[] agents, List<string> log)
            BuildEndTurnGame(int seed)
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(database, deck, seed);
            MatchController controller = new MatchController(state, database);
            List<string> log = new List<string>();
            ScriptedPlayerAgent[] agents =
            {
                new ScriptedPlayerAgent(0, ScriptedPlayerAgent.EndTurnOnce, log),
                new ScriptedPlayerAgent(1, ScriptedPlayerAgent.EndTurnOnce, log),
            };
            AgentMatchRunner runner = new AgentMatchRunner(controller, controller.View, agents);
            return (controller, runner, agents, log);
        }

        /// <summary>
        /// 空脚本（激活后等待、不自动产命令）双 agent 对局：
        /// 供需要由测试本身手动 Submit 驱动回合路由的用例。
        /// </summary>
        public static (MatchController controller, AgentMatchRunner runner, ScriptedPlayerAgent[] agents, List<string> log)
            BuildIdleGame(int seed)
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(database, deck, seed);
            MatchController controller = new MatchController(state, database);
            List<string> log = new List<string>();
            ScriptedPlayerAgent[] agents =
            {
                new ScriptedPlayerAgent(0, null, log),
                new ScriptedPlayerAgent(1, null, log),
            };
            AgentMatchRunner runner = new AgentMatchRunner(controller, controller.View, agents);
            return (controller, runner, agents, log);
        }

        public static GreedyGameResult RunGreedyGame(int seed)
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(database, deck, seed);
            MatchController controller = new MatchController(state, database);
            Dictionary<string, int> acceptedCounts = new Dictionary<string, int>();
            List<string> log = new List<string>();
            ScriptedPlayerAgent[] agents =
            {
                new ScriptedPlayerAgent(0, ctx => ScriptedPlayerAgent.GreedyTurn(ctx, database, acceptedCounts), log),
                new ScriptedPlayerAgent(1, ctx => ScriptedPlayerAgent.GreedyTurn(ctx, database, acceptedCounts), log),
            };
            AgentMatchRunner runner = new AgentMatchRunner(controller, controller.View, agents);
            runner.Start();
            return new GreedyGameResult(controller, runner, acceptedCounts, log);
        }

        public static List<(string Type, int Player, bool Accepted)> ProjectHistory(MatchController controller)
        {
            return controller.History
                .Select(r => (r.CommandType, r.PlayerId, r.Accepted))
                .ToList();
        }
    }

    /// <summary>穷举对局结果：控制器、runner、按命令类型的 accepted 计数、全局激活序列。</summary>
    internal readonly struct GreedyGameResult
    {
        public GreedyGameResult(
            MatchController controller, AgentMatchRunner runner,
            Dictionary<string, int> acceptedCounts, List<string> agentEvents)
        {
            Controller = controller;
            Runner = runner;
            AcceptedCounts = acceptedCounts;
            AgentEvents = agentEvents;
        }

        public MatchController Controller { get; }
        public AgentMatchRunner Runner { get; }
        public Dictionary<string, int> AcceptedCounts { get; }
        public List<string> AgentEvents { get; }
    }
}
