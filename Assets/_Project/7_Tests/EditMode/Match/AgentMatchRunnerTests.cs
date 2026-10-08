using System;
using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;
using static Card.Tests.EditMode.Match.AgentMatchRunnerFixtures;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T1：统一决策接口契约测试（FR-6.1 / FR-6.5）。
    /// 覆盖：构造守卫、开局激活、四类意图同源、非法命令不路由、
    /// 回合激活序列、终局停活、双算法 agent 完整对局与确定性、runner 与权威 Submit 等价。
    /// </summary>
    public class AgentMatchRunnerTests
    {
        private const int Seed = 20261008;

        // ---------- 构造守卫（AC-2） ----------

        [Test]
        public void Ctor_NullAuthority_Throws()
        {
            (MatchController controller, _, _, _) = BuildEndTurnGame(Seed);
            IPlayerAgent[] agents = SeatAgents(null!, null!);

            Assert.Throws<ArgumentNullException>(
                () => _ = new AgentMatchRunner(null!, controller.View, agents));
        }

        [Test]
        public void Ctor_NullView_Throws()
        {
            (MatchController controller, _, _, _) = BuildEndTurnGame(Seed);

            Assert.Throws<ArgumentNullException>(
                () => _ = new AgentMatchRunner(controller, null!, SeatAgents(null!, null!)));
        }

        [Test]
        public void Ctor_NullAgentList_Throws()
        {
            (MatchController controller, _, _, _) = BuildEndTurnGame(Seed);

            Assert.Throws<ArgumentNullException>(
                () => _ = new AgentMatchRunner(controller, controller.View, null!));
        }

        [Test]
        public void Ctor_NullAgentElement_ThrowsBeforeActivation()
        {
            (MatchController controller, _, _, _) = BuildEndTurnGame(Seed);
            ScriptedPlayerAgent other = new ScriptedPlayerAgent(0);

            Assert.Throws<ArgumentNullException>(
                () => _ = new AgentMatchRunner(controller, controller.View, new IPlayerAgent[] { null!, other }));
            Assert.That(other.Activations, Is.EqualTo(0));
        }

        [TestCase(3)]
        [TestCase(1)]
        public void Ctor_AgentCountNotTwo_Throws(int count)
        {
            (MatchController controller, _, _, _) = BuildEndTurnGame(Seed);
            IPlayerAgent[] agents = Enumerable.Range(0, count)
                .Select(i => (IPlayerAgent)new ScriptedPlayerAgent(i))
                .ToArray();

            Assert.Throws<ArgumentException>(
                () => _ = new AgentMatchRunner(controller, controller.View, agents));
        }

        [TestCase(0, 0)]
        [TestCase(0, 2)]
        public void Ctor_DuplicateOrOutOfRangeSeats_Throws(int first, int second)
        {
            (MatchController controller, _, _, _) = BuildEndTurnGame(Seed);
            ScriptedPlayerAgent a = new ScriptedPlayerAgent(first);
            ScriptedPlayerAgent b = new ScriptedPlayerAgent(second);

            Assert.Throws<ArgumentException>(
                () => _ = new AgentMatchRunner(controller, controller.View, new IPlayerAgent[] { a, b }));
            Assert.That(a.Activations + b.Activations, Is.EqualTo(0));
        }

        // ---------- 开局激活（AC-3） ----------

        [Test]
        public void Start_ActivatesInitialSeatExactlyOnce_WithLiveContext()
        {
            (MatchController controller, AgentMatchRunner runner, ScriptedPlayerAgent[] agents, _) =
                BuildIdleGame(Seed);
            int initialSeat = controller.View.ActivePlayerId;

            runner.Start();
            runner.Start();
            runner.Pump();

            ScriptedPlayerAgent active = agents.Single(a => a.PlayerId == initialSeat);
            ScriptedPlayerAgent idle = agents.Single(a => a.PlayerId != initialSeat);
            Assert.That(active.Activations, Is.EqualTo(1));
            Assert.That(idle.Activations, Is.EqualTo(0));
            Assert.That(active.LastContext!.ActivePlayerId, Is.EqualTo(initialSeat));
            Assert.That(active.LastContext.View, Is.SameAs(controller.View));
        }

        // ---------- 四类意图同源（AC-4） ----------

        [Test]
        public void AlgorithmAgent_AllFourCommandTypes_AreAcceptedViaContext()
        {
            GreedyGameResult result = RunGreedyGame(Seed);

            Assert.That(result.Controller.IsFinished, Is.True, "穷举 agent 应能靠疲劳打完一局");
            Assert.That(result.AcceptedCounts.GetValueOrDefault("PlayCardCommand"), Is.GreaterThanOrEqualTo(1));
            Assert.That(result.AcceptedCounts.GetValueOrDefault("AttackCommand"), Is.GreaterThanOrEqualTo(1));
            Assert.That(result.AcceptedCounts.GetValueOrDefault("UseHeroPowerCommand"), Is.GreaterThanOrEqualTo(1));
            Assert.That(result.AcceptedCounts.GetValueOrDefault("EndTurnCommand"), Is.GreaterThanOrEqualTo(1));
        }

        // ---------- 非法命令不路由（AC-5） ----------

        [Test]
        public void Submit_InvalidCommand_StateAndActivationUnchanged()
        {
            (MatchController controller, AgentMatchRunner runner, ScriptedPlayerAgent[] agents, _) =
                BuildIdleGame(Seed);
            runner.Start();
            int activeSeat = controller.View.ActivePlayerId;
            int otherSeat = 1 - activeSeat;
            int turnBefore = controller.View.TurnNumber;
            int handCountBefore = controller.View.GetPlayer(activeSeat).Hand.Cards.Count;
            int eventsBefore = agents.Sum(a => a.Events.Count);

            CommandResult wrongSeat = runner.Submit(new EndTurnCommand(otherSeat));
            CommandResult missingCard = runner.Submit(new PlayCardCommand(activeSeat, int.MaxValue, TargetRef.None));

            Assert.That(wrongSeat.IsInvalid, Is.True);
            Assert.That(wrongSeat.Error, Is.EqualTo(CommandError.NotYourTurn));
            Assert.That(missingCard.IsInvalid, Is.True);
            Assert.That(controller.View.ActivePlayerId, Is.EqualTo(activeSeat));
            Assert.That(controller.View.TurnNumber, Is.EqualTo(turnBefore));
            Assert.That(controller.View.GetPlayer(activeSeat).Hand.Cards.Count, Is.EqualTo(handCountBefore));
            Assert.That(agents.Sum(a => a.Events.Count), Is.EqualTo(eventsBefore));
        }

        // ---------- 回合激活序列（AC-6） ----------

        [Test]
        public void AcceptedEndTurn_RoutesDeactivateThenActivate_InOrder()
        {
            (MatchController controller, AgentMatchRunner runner, _, List<string> log) = BuildIdleGame(Seed);
            runner.Start();
            int s0 = controller.View.ActivePlayerId;
            int s1 = 1 - s0;

            runner.Submit(new EndTurnCommand(s0));
            runner.Submit(new EndTurnCommand(s1));

            Assert.That(log.TakeLast(5),
                Is.EqualTo(new List<string> { "A" + s0, "D" + s0, "A" + s1, "D" + s1, "A" + s0 }));
        }

        [Test]
        public void Pump_IsIdempotent_AndResyncsAfterExternalAuthoritySubmit()
        {
            (MatchController controller, AgentMatchRunner runner, _, List<string> log) = BuildIdleGame(Seed);
            runner.Start();
            int s0 = controller.View.ActivePlayerId;
            int s1 = 1 - s0;

            runner.Pump();
            Assert.That(log, Is.EqualTo(new List<string> { "A" + s0 }));

            // 外部直接走权威 Submit（绕过 runner）后，Pump 补做激活路由。
            controller.Submit(new EndTurnCommand(s0));
            runner.Pump();
            runner.Pump();

            Assert.That(log,
                Is.EqualTo(new List<string> { "A" + s0, "D" + s0, "A" + s1 }));
        }

        // ---------- 终局停活（AC-7） ----------

        [Test]
        public void MatchFinish_DeactivatesCurrentSeat_AndRejectsLaterSubmit()
        {
            (MatchController controller, AgentMatchRunner runner, ScriptedPlayerAgent[] agents, List<string> log) =
                BuildEndTurnGame(Seed);

            runner.Start(); // 同步重入：EndTurn 链一路打到疲劳终局

            Assert.That(controller.IsFinished, Is.True);
            Assert.That(controller.LastOutcome, Is.Not.Null);
            Assert.That(agents[0].Activations, Is.EqualTo(agents[0].Deactivations));
            Assert.That(agents[1].Activations, Is.EqualTo(agents[1].Deactivations));
            // 疲劳可发生在死者回合的抽牌瞬间：最后停活的是提交结束回合的座位，
            // 死者座位可能尚未激活；终局后事件序列必须以停活收尾。
            Assert.That(log.Last()[0], Is.EqualTo('D'));

            CommandResult afterFinish = runner.Submit(
                new EndTurnCommand(controller.View.ActivePlayerId));
            Assert.That(afterFinish.IsInvalid, Is.True);
            Assert.That(log.Count, Is.GreaterThan(0));
            Assert.That(agents.Sum(a => a.Events.Count), Is.EqualTo(log.Count));
        }

        // ---------- 双 agent 完整对局 + 确定性（AC-8） ----------

        [Test]
        public void TwoAlgorithmAgents_DriveRealMatchToFinish_OnlyFourCommandTypes()
        {
            GreedyGameResult result = RunGreedyGame(Seed + 1);

            Assert.That(result.Controller.IsFinished, Is.True);
            HashSet<string> commandTypes = result.Controller.History
                .Select(r => r.CommandType)
                .ToHashSet();
            Assert.That(commandTypes, Is.SubsetOf(new HashSet<string>
            {
                "PlayCardCommand", "AttackCommand", "UseHeroPowerCommand", "EndTurnCommand",
            }));
        }

        [Test]
        public void SameSeed_TwoGreedyGames_ProduceIdenticalRouteAndHistory()
        {
            GreedyGameResult first = RunGreedyGame(Seed + 2);
            GreedyGameResult second = RunGreedyGame(Seed + 2);

            Assert.That(first.AgentEvents, Is.EqualTo(second.AgentEvents));
            Assert.That(ProjectHistory(first.Controller), Is.EqualTo(ProjectHistory(second.Controller)));
            Assert.That(first.Controller.LastOutcome!.Result,
                Is.EqualTo(second.Controller.LastOutcome!.Result));
        }

        // ---------- 与权威 Submit 等价（接口语义 5） ----------

        [Test]
        public void RunnerSubmit_ProducesSameStateAndHistory_AsDirectAuthority()
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState directState = MatchControllerFixtures.NewMatch(database, deck, Seed);
            MatchState routedState = MatchControllerFixtures.NewMatch(database, deck, Seed);
            MatchController direct = new MatchController(directState, database);
            MatchController routed = new MatchController(routedState, database);
            AgentMatchRunner runner = new AgentMatchRunner(
                routed, routed.View, SeatAgents(null!, null!));
            runner.Start();
            int firstSeat = direct.View.ActivePlayerId;

            for (int step = 0; step < 6; step++)
            {
                EndTurnCommand command = new EndTurnCommand((firstSeat + step) % 2);
                direct.Submit(command);
                runner.Submit(command);
            }

            MatchStateComparer.AssertEqual(direct.State, routed.State);
            Assert.That(ProjectHistory(direct), Is.EqualTo(ProjectHistory(routed)));
        }

        // ---------- 激活/停活全程配对（AC-6/AC-7 补充） ----------

        [Test]
        public void FatigueGame_EveryActivationIsPairedWithDeactivation()
        {
            (_, AgentMatchRunner runner, ScriptedPlayerAgent[] agents, _) = BuildEndTurnGame(Seed);
            runner.Start(); // EndTurnOnce 链级联到终局

            foreach (ScriptedPlayerAgent agent in agents)
            {
                Assert.That(agent.Deactivations, Is.EqualTo(agent.Activations));
                Assert.That(agent.Activations, Is.GreaterThan(0));
            }
        }

    }
}
