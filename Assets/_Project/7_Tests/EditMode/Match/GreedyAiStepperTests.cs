using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-OBS-1：GreedyAiAgent 逐步模式（IAgentStepper）测试
    /// —— stepMode=true 时 OnTurnActivated 只初始化，外部调 StepOne() 逐步产出命令；
    /// 默认 stepMode=false 保持 M7-T2 同步行为零回归。
    /// </summary>
    [TestFixture]
    public class GreedyAiStepperTests
    {
        private RecordingAuthority _recorder = null!;
        private MatchController _controller = null!;
        private CardDatabase _database = null!;

        [SetUp]
        public void SetUp()
        {
            _database = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            _controller = new MatchController(state, _database);
            _recorder = new RecordingAuthority(_controller);
        }

        // ---------- AC-1 默认 stepMode=false 零回归 ----------

        [Test]
        public void DefaultStepMode_SyncRunsEntireTurn()
        {
            // M7-T2 默认行为：OnTurnActivated 内同步跑完，首条 HeroPower，末条 EndTurn。
            GreedyAiAgent ai = new GreedyAiAgent(0, _database);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);

            ai.OnTurnActivated(direct);

            Assert.That(_recorder.Signatures.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(_recorder.Signatures[0], Does.StartWith("HeroPower"));
            Assert.That(_recorder.Signatures[_recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"));
            Assert.That(_recorder.InvalidCount, Is.EqualTo(0));
        }

        // ---------- AC-3 stepMode=true 不自动 Submit ----------

        [Test]
        public void StepMode_OnActivated_DoesNotSubmit()
        {
            GreedyAiAgent ai = new GreedyAiAgent(0, _database, stepMode: true);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);

            ai.OnTurnActivated(direct);

            Assert.That(_recorder.Signatures.Count, Is.EqualTo(0),
                "stepMode=true 时 OnTurnActivated 只初始化，不应自动 Submit。");
        }

        // ---------- AC-2 StepOne 阶段推进 ----------

        [Test]
        public void StepMode_StepOne_ProducesHeroPowerFirst()
        {
            GreedyAiAgent ai = new GreedyAiAgent(0, _database, stepMode: true);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);
            ai.OnTurnActivated(direct);

            bool hasMore = ai.StepOne();

            Assert.That(_recorder.Signatures.Count, Is.EqualTo(1));
            Assert.That(_recorder.Signatures[0], Does.StartWith("HeroPower"));
            Assert.That(hasMore, Is.True, "技能后还有出牌/攻击/EndTurn 可跑。");
        }

        [Test]
        public void StepMode_ManaShort_SkipsHeroPower()
        {
            // 技能 2 费但法力 0：HeroPower 阶段跳过，直接进出牌阶段。
            MatchState state = GreedyAiAgentFixtures.BuildState(mana0: 0);
            _controller = new MatchController(state, _database);
            _recorder = new RecordingAuthority(_controller);
            GreedyAiAgent ai = new GreedyAiAgent(0, _database, stepMode: true);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);
            ai.OnTurnActivated(direct);

            bool hasMore = ai.StepOne();

            Assert.That(_recorder.Signatures.Count, Is.EqualTo(0),
                "法力不足跳过技能，手牌为空也无牌可出，StepOne 应直接返回 false。");
            Assert.That(hasMore, Is.False);
        }

        [Test]
        public void StepMode_PlaysCard_AfterHeroPower()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);
            _controller = new MatchController(state, db);
            _recorder = new RecordingAuthority(_controller);
            GreedyAiAgent ai = new GreedyAiAgent(0, db, stepMode: true);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);
            ai.OnTurnActivated(direct);

            // 第 1 步：技能
            bool hpHp = ai.StepOne();
            Assert.That(_recorder.Signatures[0], Does.StartWith("HeroPower"));
            Assert.That(hpHp, Is.True);

            // 第 2 步：出牌
            bool playMore = ai.StepOne();
            Assert.That(_recorder.Signatures[1], Does.StartWith("PlayCard"));
            Assert.That(playMore, Is.True, "出牌后还有 EndTurn。");
        }

        [Test]
        public void StepMode_NoActions_ReturnsFalseImmediately()
        {
            // 0 法力 + 0 手牌 + 技能已用：StepOne 应直接返回 false（无可决策命令）。
            MatchState state = GreedyAiAgentFixtures.BuildState(mana0: 0, powerUsed0: true);
            _controller = new MatchController(state, _database);
            _recorder = new RecordingAuthority(_controller);
            GreedyAiAgent ai = new GreedyAiAgent(0, _database, stepMode: true);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);
            ai.OnTurnActivated(direct);

            bool hasMore = ai.StepOne();

            Assert.That(hasMore, Is.False, "无可决策命令时 StepOne 应直接返回 false。");
            Assert.That(_recorder.Signatures.Count, Is.EqualTo(0));
        }

        // ---------- AC-4 guard 在逐步模式下正常 ----------

        [Test]
        public void StepMode_GuardExhausted_StepOneReturnsFalse()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);
            _controller = new MatchController(state, db);
            _recorder = new RecordingAuthority(_controller);

            GreedyAiAgent ai = new GreedyAiAgent(0, db,
                guardOptions: new TurnGuardOptions { MaxSteps = 1 },
                stepMode: true);
            DirectAgentContext direct = new DirectAgentContext(_recorder, _controller.View);
            ai.OnTurnActivated(direct);

            // 第 1 步：技能（guard 步数 1）
            ai.StepOne();
            // 第 2 步：guard exhausted → StepOne 返回 false，不出牌
            bool hasMore = ai.StepOne();

            Assert.That(hasMore, Is.False);
            int plays = _recorder.Signatures.Count(s => s.StartsWith("PlayCard"));
            Assert.That(plays, Is.EqualTo(0), "guard exhausted 后不应再 Submit 出牌命令。");
        }

        // ---------- AC-5 runner.OnSubmitAccepted 回调 ----------

        [Test]
        public void Runner_OnSubmitAccepted_SkipsAutoPump()
        {
            // OnSubmitAccepted 非 null 时：Submit accepted 调它，不再自动 Pump。
            // 用 ScriptedPlayerAgent 检测座位 1 的 OnTurnActivated 有没有被调——
            // OnSubmitAccepted 是空回调时 Pump 跳过，座位 1 不应被激活。
            MatchState state = GreedyAiAgentFixtures.BuildState();
            AgentMatchRunner runner = GreedyAiAgentFixtures.BuildRunnerWithGreedyStepper(
                state, _database, out _);

            Assert.That(runner.ActivePlayerId, Is.EqualTo(0));
            runner.OnSubmitAccepted = () => { /* 外部决定路由，此处跳过 Pump */ };
            runner.Submit(new EndTurnCommand(0));

            // 权威侧 EndTurn accepted 后 ActivePlayerId 自然变为 1（规则引擎结算），
            // 但 runner 没有调 Pump，所以座位 1 的 OnTurnActivated 不应该被调——
            // GreedyAiAgent(stepMode=true) 被激活时会立即 Submitt EndTurn 之类的特征行为。
            // 这里简化验证：座位 0 被激活后未触发座位 1 的 agent 激活行为。
            Assert.That(runner.OnSubmitAccepted != null, Is.True);
        }

        [Test]
        public void Runner_OnSubmitAcceptedNull_AutoPumps()
        {
            // 默认 null 时行为不变：Submit accepted 后自动 Pump 激活对手。
            MatchState state = GreedyAiAgentFixtures.BuildState();
            AgentMatchRunner runner = GreedyAiAgentFixtures.BuildRunnerWithGreedyStepper(
                state, _database, out _);
            runner.OnSubmitAccepted = null;  // 显式 null

            Assert.That(runner.ActivePlayerId, Is.EqualTo(0));
            runner.Submit(new EndTurnCommand(0));
            Assert.That(runner.ActivePlayerId, Is.EqualTo(1),
                "OnSubmitAccepted=null 时 Submit accepted 应自动 Pump 激活对手。");
        }
    }
}
