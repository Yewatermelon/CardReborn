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
    /// M7-T3 GreedyAiAgent × TurnGuard 集成（FR-6.4 完成标准：任何局面下 AI 回合必然结束）：
    /// 守卫触发后决策停止，但 EndTurn 必发（EndTurn 不受守卫约束）。
    /// </summary>
    [TestFixture]
    public class GreedyAiAgentGuardTests
    {
        [Test]
        public void MaxSteps_TruncatesDecision_AndEndsTurn()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card5Cost), 102);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, db, out _,
                guardOptions: new TurnGuardOptions { MaxSteps = 2 });

            int decisions = recorder.Signatures.Count(s => !s.StartsWith("EndTurn"));
            Assert.That(decisions, Is.EqualTo(2), "步数上限 2 应只允许技能 + 一次出牌。");
            Assert.That(recorder.Signatures[recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"),
                "守卫触发后仍必须以 EndTurn 收尾（必然结束）。");
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void SpellArmorLoop_StoppedByStepLimit()
        {
            // 10 张 1 费无目标叠甲法术：无进展检测不触发（护甲每步 +1，局面持续变化），
            // 只能靠步数上限兜底——三层独立性的真实局面验证。
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            for (int i = 0; i < 10; i++)
            {
                RuleEngineTestHelpers.AddToHand(
                    state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.SpellArmor), 300 + i);
            }

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, db, out _,
                guardOptions: new TurnGuardOptions { MaxSteps = 5 });

            int decisions = recorder.Signatures.Count(s => !s.StartsWith("EndTurn"));
            Assert.That(decisions, Is.EqualTo(5), "有牌可出也要在步数上限处停下（技能 1 + 出牌 4）。");
            Assert.That(recorder.Signatures.Count(s => s.StartsWith("PlayCard")), Is.EqualTo(4));
            Assert.That(recorder.Signatures[recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void OneStepLimit_StillEndsTurn()
        {
            // 极限配置：只允许 1 条决策命令，回合同样必然结束。
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, db, out _,
                guardOptions: new TurnGuardOptions { MaxSteps = 1 });

            int decisions = recorder.Signatures.Count(s => !s.StartsWith("EndTurn"));
            Assert.That(decisions, Is.EqualTo(1), "上限 1 = 仅首条技能命令。");
            Assert.That(recorder.Signatures[0], Does.StartWith("HeroPower"));
            Assert.That(recorder.Signatures[recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void TimeLimit_TruncatesDecision_AndEndsTurn()
        {
            // 分帧泵时钟推进预演：上下文装饰器在每次提交前推进 1 tick（OBS-1 的真实接线形态）。
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            for (int i = 0; i < 10; i++)
            {
                RuleEngineTestHelpers.AddToHand(
                    state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.SpellArmor), 300 + i);
            }

            MatchController controller = new MatchController(state, db);
            ManualClock clock = new ManualClock();
            GreedyAiAgent ai = new GreedyAiAgent(0, db, new TurnGuardOptions { MaxTicks = 3 }, clock);
            RecordingAuthority recorder = new RecordingAuthority(controller);
            DirectAgentContext direct = new DirectAgentContext(recorder, controller.View);

            ai.OnTurnActivated(new ClockTickingContext(direct, clock));

            int decisions = recorder.Signatures.Count(s => !s.StartsWith("EndTurn"));
            Assert.That(decisions, Is.EqualTo(3), "每次提交推进 1 tick、预算 3：第 3 步注册后应触发时间上限。");
            Assert.That(recorder.Signatures[recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"),
                "时间上限触发后仍必须以 EndTurn 收尾。");
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void DefaultGuardOptions_T2BehaviorUnchanged()
        {
            // 显式 (null, null) 走新构造：默认守卫语义必须与 M7-T2 完全一致（AC-8）。
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, db, out _, guardOptions: null, clock: null);

            Assert.That(recorder.Signatures[0], Is.EqualTo("HeroPower|0|Hero:1"));
            Assert.That(recorder.Signatures[recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        /// <summary>
        /// 时钟推进上下文装饰器：每次 Submit 前推进 1 tick，模拟分帧泵下"命令之间时间流逝"。
        /// </summary>
        private sealed class ClockTickingContext : IAgentContext
        {
            private readonly IAgentContext _inner;
            private readonly ManualClock _clock;

            public ClockTickingContext(IAgentContext inner, ManualClock clock)
            {
                _inner = inner;
                _clock = clock;
            }

            public int ActivePlayerId => _inner.ActivePlayerId;

            public IReadOnlyMatchState View => _inner.View;

            public CommandResult Submit(IGameCommand command)
            {
                _clock.Advance(1);
                return _inner.Submit(command);
            }
        }
    }
}
