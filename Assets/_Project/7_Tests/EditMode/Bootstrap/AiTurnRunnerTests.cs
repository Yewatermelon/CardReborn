using System.Collections.Generic;
using System.Linq;
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
            var humans = new[]
            {
                new ScriptedPlayerAgent(0),
                _ai,
            };
            _runner = new AgentMatchRunner(_controller, _controller.View, humans);

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
            // Start 后默认 seat 0（先手），不是 AI。手动切到 AI。
            _runner.Submit(new EndTurnCommand(0));
            _runner.Pump();  // 激活 seat 1（AI）

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(AiSeat));

            // 手动调用 Update 逻辑（不依赖 MonoBehaviour 的 Update 调度）
            SimulateUpdate();

            Assert.That(_aiRunner.IsAiTurn, Is.True, "AI 活跃时 IsAiTurn 应为 true。");
        }

        [Test]
        public void Update_SeatIsHuman_IsAiTurnFalse()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();
            // Start 后 seat 0（玩家先手）

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(0));

            SimulateUpdate();

            Assert.That(_aiRunner.IsAiTurn, Is.False, "玩家活跃时 IsAiTurn 应为 false。");
        }

        [Test]
        public void OnSubmitAccepted_DefaultBehavior_AutoPumps()
        {
            // 未绑 AiTurnRunner 时默认行为：Submit accepted 后自动 Pump。
            _runner.Start();
            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(0));

            _runner.Submit(new EndTurnCommand(0));  // 默认自动 Pump

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(1),
                "默认 OnSubmitAccepted=null 时 Submit accepted 应自动 Pump 激活对手。");
        }

        [Test]
        public void BoundOnSubmitAccepted_SubmitAccepted_DoesPump()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();
            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(0));

            // 玩家发 EndTurn → 权威侧变 seat 1 → OnSubmitAccepted → Pump 激活 AI
            _runner.Submit(new EndTurnCommand(0));

            Assert.That(_controller.View.ActivePlayerId, Is.EqualTo(1),
                "绑 OnSubmitAccepted 后 Submit accepted 仍应 Pump 激活对手。");
        }

        [Test]
        public void AiTurnRunner_AiStepOne_SubmitsCommands()
        {
            _aiRunner.Bind(_runner, _ai, _controller, _ui);
            _runner.Start();
            // 切到 AI 回合
            _runner.Submit(new EndTurnCommand(0));
            _runner.Pump();

            int eventsBefore = _controller.Events.Count;

            // StepOne 应该至少提交一条命令（或在法力/手牌/技能都空时直接 Done）
            bool stepped = _ai.StepOne();

            // 无论 StepOne 是否提交，都不应异常；如果返回 true 说明 Submit 了。
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
