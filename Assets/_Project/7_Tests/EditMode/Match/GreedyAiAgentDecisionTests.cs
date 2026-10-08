using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T2 AC-5/AC-6：GreedyAiAgent 技能与出牌决策（手工局面 + 迷你配置库）。
    /// 局面里座位 0 英雄技能为"任意目标打 1"，座位 1 为"无目标叠甲"。
    /// </summary>
    [TestFixture]
    public class GreedyAiAgentDecisionTests
    {
        [Test]
        public void HeroPower_UsedFirst_WhenAffordable()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            Assert.That(recorder.Signatures[0], Is.EqualTo("HeroPower|0|Hero:1"),
                "技能可用且有法力时，首条命令必须是 UseHeroPower（无随从可指时打敌英雄）。");
            Assert.That(recorder.Signatures[recorder.Signatures.Count - 1], Is.EqualTo("EndTurn|0"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void HeroPower_Skipped_WhenManaShort()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState(mana0: 1);
            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            Assert.That(recorder.Signatures.Any(s => s.StartsWith("HeroPower")), Is.False);
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void HeroPower_Skipped_WhenAlreadyUsed()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState(powerUsed0: true);
            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            Assert.That(recorder.Signatures.Any(s => s.StartsWith("HeroPower")), Is.False);
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void Play_HighestCostFirst()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card5Cost), 102);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(state, db, out _);

            List<string> plays = recorder.Signatures
                .Where(s => s.StartsWith("PlayCard")).ToList();
            Assert.That(plays.Count, Is.EqualTo(2), "10 费下两张牌都应出完。");
            Assert.That(plays[0], Does.Contain("card=102"), "必须先出费用最高的 5 费卡。");
            Assert.That(plays[1], Does.Contain("card=101"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void Play_SkipsMinion_WhenBoardFull()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            PlayerState self = state.GetPlayer(0);
            for (int i = 0; i < 7; i++)
            {
                RuleEngineTestHelpers.AddToBoard(self, MatchTestCards.Minion("FILLER_" + i), 200 + i);
            }

            RuleEngineTestHelpers.AddToHand(self, db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);
            RuleEngineTestHelpers.AddToHand(self, db.RequireCard(GreedyAiAgentFixtures.SpellArmor), 102);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(state, db, out _);

            List<string> plays = recorder.Signatures
                .Where(s => s.StartsWith("PlayCard")).ToList();
            Assert.That(plays.Count, Is.EqualTo(1), "场面满 7 时只能出无目标法术。");
            Assert.That(plays[0], Does.Contain("card=102"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0), "不允许出现 BoardFull 被拒。");
        }

        [Test]
        public void Play_EnemyMinionRule_TargetsHighestAttackNonStealth()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            PlayerState enemy = state.GetPlayer(1);
            RuleEngineTestHelpers.AddToBoard(enemy, MatchTestCards.Minion("SNEAK", 1, 1, 1, Keyword.Stealth), 120);
            RuleEngineTestHelpers.AddToBoard(enemy, MatchTestCards.Minion("BRUTE", 5, 5, 5), 121);
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.SpellEnemyMinion), 101);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(state, db, out _);

            List<string> plays = recorder.Signatures
                .Where(s => s.StartsWith("PlayCard")).ToList();
            Assert.That(plays.Count, Is.EqualTo(1));
            Assert.That(plays[0], Does.Contain("Minion:121"),
                "EnemyMinion 伤害法术必须指向最高攻非潜行敌方随从。");
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void Play_EnemyMinionRule_Skipped_WhenOnlyStealthMinions()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(
                state.GetPlayer(1), MatchTestCards.Minion("SNEAK", 1, 1, 1, Keyword.Stealth), 120);
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.SpellEnemyMinion), 101);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(state, db, out _);

            Assert.That(recorder.Signatures.Any(s => s.StartsWith("PlayCard")), Is.False,
                "唯一合法目标是潜行随从时，AI 不得指它（潜行永不成为目标）。");
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void TurnEndsImmediately_WhenNothingToDo()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState(mana0: 0);
            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            CollectionAssert.AreEqual(new[] { "EndTurn|0" }, recorder.Signatures);
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectedCandidate_IsNotRetriedThisTurn()
        {
            CardDatabase db = GreedyAiAgentFixtures.BuildDatabase();
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 101);
            RuleEngineTestHelpers.AddToHand(state.GetPlayer(0), db.RequireCard(GreedyAiAgentFixtures.Card2Cost), 102);
            MatchController controller = new MatchController(state, db);
            GreedyAiAgent ai = new GreedyAiAgent(0, db);
            IAgentContext direct = new DirectAgentContext(controller, controller.View);
            FailPlaysContext context = new FailPlaysContext(direct, failPlays: 1);

            ai.OnTurnActivated(context);

            Assert.That(context.FailedPlays, Is.EqualTo(1), "注入的一次拒绝应被消费。");
            List<string> plays = controller.History
                .Where(r => r.CommandType == nameof(PlayCardCommand))
                .Select(r => r.CommandType + "|" + r.PlayerId)
                .ToList();
            Assert.That(plays.Count, Is.EqualTo(1), "被拒的同一张卡不得重试；另一张应正常出。");
            Assert.That(controller.History.Any(r => !r.Accepted), Is.False,
                "注入拒绝不经过权威侧，权威台账应保持零被拒。");
        }
    }
}
