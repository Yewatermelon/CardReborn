using System.Collections.Generic;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Match;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// M7-OBS-1：AiTurnRunner（人机 AI 分帧驱动器）EditMode 测试。
    /// 仅 EditMode 可跑（依赖 MonoBehaviour 组件 + Unity Test Framework）。
    /// </summary>
    [TestFixture]
    public sealed class AiTurnRunnerTests
    {
        private const int Seed = 20260101;
        private const int AiSeat = 1;

        private GameObject _host = null!;
        private CardDatabase _database = null!;
        private AgentMatchRunner _runner = null!;
        private GreedyAiAgent _ai = null!;
        private MatchController _controller = null!;
        private BattleUi _ui = null!;
        private AiTurnRunner _aiRunner = null!;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AiTurnRunner_Test");
            _database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(_database, deck, Seed);
            _controller = new MatchController(state, _database);

            _ai = new GreedyAiAgent(AiSeat, _database, stepMode: true);
            IPlayerAgent[] agents =
            {
                new ScriptedPlayerAgent(0),
                _ai,
            };
            _runner = new AgentMatchRunner(_controller, _controller.View, agents);

            _ui = new BattleUi();
            _aiRunner = _host.AddComponent<AiTurnRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void Bind_SetsOnSubmitAccepted()
        {
            Assert.That(_runner.OnSubmitAccepted, Is.Null);

            _aiRunner.Bind(_runner, _ai, _controller, _ui);

            Assert.That(_runner.OnSubmitAccepted, Is.Not.Null);
        }

        [Test]
        public void Update_SeatIsAi_IsAiTurnTrue()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();

            // 切到 AI 活跃：如果先手是 AI 直接跳过，否则先手发 EndTurn 切过去。
            int firstSeat = _controller.View.ActivePlayerId;
            if (firstSeat != AiSeat)
            {
                _runner.Submit(new EndTurnCommand(firstSeat));
            }

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(AiSeat));

            SimulateUpdate();

            Assert.That(_aiRunner.IsAiTurn, Is.True, "AI 活跃时 IsAiTurn 应为 true。");
        }

        [Test]
        public void Update_SeatIsHuman_IsAiTurnFalse()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();
            int active = _controller.View.ActivePlayerId;

            // 如果先手就是 AI（取决于种子），需要先 EndTurn 切到人类再断言。
            if (active == AiSeat)
            {
                _runner.Submit(new EndTurnCommand(AiSeat));
            }

            Assert.That(_controller.View.ActivePlayerId, Is.Not.EqualTo(AiSeat));

            SimulateUpdate();

            Assert.That(_aiRunner.IsAiTurn, Is.False, "人类活跃时 IsAiTurn 应为 false。");
        }

        [Test]
        public void OnSubmitAccepted_DefaultBehavior_AutoPumps()
        {
            _runner.Start();
            int firstSeat = _controller.View.ActivePlayerId;
            Assert.That(firstSeat, Is.LessThan(2), "先手必须是 0 或 1");

            _runner.Submit(new EndTurnCommand(firstSeat));

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(1 - firstSeat),
                "默认 OnSubmitAccepted=null 时 Submit accepted 应自动 Pump 激活对手。");
        }

        [Test]
        public void BoundOnSubmitAccepted_SubmitAccepted_DoesPump()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();
            int firstSeat = _controller.View.ActivePlayerId;
            Assert.That(firstSeat, Is.LessThan(2), "先手必须是 0 或 1");

            _runner.Submit(new EndTurnCommand(firstSeat));

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(1 - firstSeat),
                "绑 OnSubmitAccepted 后 Submit accepted 仍应 Pump 激活对手。");
        }

        [Test]
        public void AiTurnRunner_AiStepOne_SubmitsCommands()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();

            // 切到 AI 活跃
            int firstSeat = _controller.View.ActivePlayerId;
            if (firstSeat != AiSeat)
            {
                _runner.Submit(new EndTurnCommand(firstSeat));
            }

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(AiSeat));

            int eventsBefore = _controller.Events.Count;
            bool stepped = _ai.StepOne();

            if (stepped)
            {
                Assert.That(_controller.Events.Count, Is.GreaterThan(eventsBefore),
                    "StepOne 返回 true 后应产生事件。");
            }
        }

        /// <summary>
        /// AiTurnRunner.Update 不直接 MonoBehaviour.Update，用反射调私有方法模拟。
        /// 这样可以在 EditMode 里精确控制 Update 时机。
        /// </summary>
        private void SimulateUpdate()
        {
            var method = typeof(AiTurnRunner).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_aiRunner, null);
        }
    }
}
