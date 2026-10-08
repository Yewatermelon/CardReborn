using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle.Input;
using Card.Presentation.Battle.Targeting;
using Card.Tests.EditMode.Match;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// M7-T1（AC-9）：人类 agent 激活回调必须把 PlayerInputController 与
    /// TargetingController 的本地座位路由到当前行动方；命令仍由输入组件上行，
    /// 与热座实盘接线（BattleSceneBootstrap.BindInput）同构。
    /// 仅 EditMode 可跑（依赖 MonoBehaviour 组件），不进 kernel 工具链。
    /// </summary>
    [TestFixture]
    public sealed class HumanPlayerAgentTests
    {
        private const int Seed = 20260101;

        private GameObject _host = null!;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("HumanPlayerAgent_Test");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void Start_ActivatesActiveSeat_BothInputControllersPointToSeat()
        {
            (MatchController controller, AgentMatchRunner runner, _, PlayerInputController input,
                TargetingController targeting) = BuildRunner();

            runner.Start();
            int seat = controller.View.ActivePlayerId;

            Assert.That(input._localPlayerId, Is.EqualTo(seat));
            Assert.That(targeting._localPlayerId, Is.EqualTo(seat));
        }

        [Test]
        public void EndTurnFromInput_RoutesActivation_LocalSeatFollowsActivePlayer()
        {
            (MatchController controller, AgentMatchRunner runner, _, PlayerInputController input,
                TargetingController targeting) = BuildRunner();
            runner.Start();
            int s0 = controller.View.ActivePlayerId;
            int s1 = 1 - s0;

            input.NotifyEndTurnClicked();

            Assert.That(controller.View.ActivePlayerId, Is.EqualTo(s1));
            Assert.That(input._localPlayerId, Is.EqualTo(s1));
            Assert.That(targeting._localPlayerId, Is.EqualTo(s1));

            input.NotifyEndTurnClicked();

            Assert.That(controller.View.ActivePlayerId, Is.EqualTo(s0));
            Assert.That(input._localPlayerId, Is.EqualTo(s0));
            Assert.That(targeting._localPlayerId, Is.EqualTo(s0));
        }

        [Test]
        public void RejectedCommand_DoesNotRerouteSeat()
        {
            (MatchController controller, AgentMatchRunner runner, _, PlayerInputController input,
                TargetingController targeting) = BuildRunner();
            runner.Start();
            int seatBefore = controller.View.ActivePlayerId;
            int rejections = 0;
            input.CommandRejected += _ => rejections++;

            input.NotifyHandCardClicked(int.MaxValue); // 不存在的实例，权威必拒

            Assert.That(rejections, Is.EqualTo(1));
            Assert.That(controller.View.ActivePlayerId, Is.EqualTo(seatBefore));
            Assert.That(input._localPlayerId, Is.EqualTo(seatBefore));
            Assert.That(targeting._localPlayerId, Is.EqualTo(seatBefore));
        }

        [Test]
        public void Deactivate_IsSafeAndKeepsSeat()
        {
            (MatchController controller, AgentMatchRunner runner, HumanPlayerAgent[] humans,
                PlayerInputController input, TargetingController targeting) = BuildRunner();
            runner.Start();
            int seat = controller.View.ActivePlayerId;

            Assert.DoesNotThrow(() => humans[seat].OnTurnDeactivated());
            Assert.That(input._localPlayerId, Is.EqualTo(seat));
            Assert.That(targeting._localPlayerId, Is.EqualTo(seat));
        }

        [Test]
        public void PlayerId_ReflectsConstructorSeat()
        {
            (_, _, HumanPlayerAgent[] humans, _, _) = BuildRunner();

            Assert.That(humans[0].PlayerId, Is.EqualTo(0));
            Assert.That(humans[1].PlayerId, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_NullDependenciesOrBadSeat_Throws()
        {
            var input = _host.AddComponent<PlayerInputController>();
            var targeting = _host.AddComponent<TargetingController>();

            Assert.Throws<ArgumentNullException>(() => new HumanPlayerAgent(0, null!, targeting));
            Assert.Throws<ArgumentNullException>(() => new HumanPlayerAgent(0, input, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HumanPlayerAgent(-1, input, targeting));
        }

        private (MatchController controller, AgentMatchRunner runner, HumanPlayerAgent[] humans,
            PlayerInputController input, TargetingController targeting) BuildRunner()
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(database, deck, Seed);
            var controller = new MatchController(state, database);

            var input = _host.AddComponent<PlayerInputController>();
            var targeting = _host.AddComponent<TargetingController>();
            input._localPlayerId = 0;
            targeting._localPlayerId = 0;

            var humans = new HumanPlayerAgent[]
            {
                new HumanPlayerAgent(0, input, targeting),
                new HumanPlayerAgent(1, input, targeting),
            };
            var runner = new AgentMatchRunner(controller, controller.View, humans);
            input.Initialize(new MatchControllerCommandSink(runner));
            return (controller, runner, humans, input, targeting);
        }
    }
}
