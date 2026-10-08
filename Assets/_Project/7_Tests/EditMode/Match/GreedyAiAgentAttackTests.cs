using System.Linq;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T2 AC-7/AC-8：GreedyAiAgent 攻击决策——嘲讽优先、有利交换、
    /// 潜行豁免、召唤失调/冻结/冲锋/风怒资格。
    /// 座位 0 技能为"任意目标打 1"：本文件敌方随从生命均 > 1 或潜行，技能一律打脸，不干扰攻击断言。
    /// </summary>
    [TestFixture]
    public class GreedyAiAgentAttackTests
    {
        [Test]
        public void Attack_Taunt_PicksLowestHealthTaunt()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("ATK", 3, 3, 3), 110, summoningSick: false);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("T_A", 2, 3, 2, Keyword.Taunt), 120);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("T_B", 2, 2, 2, Keyword.Taunt), 121);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("BIG", 5, 5, 5), 122);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertAttack(recorder, "Attack|0|attacker=110|Minion:121",
                "多个嘲讽必须选血最少的。");
        }

        [Test]
        public void Attack_FavorableTrade_TargetsMinion()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("ATK", 4, 4, 4), 110, summoningSick: false);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("PREY", 3, 2, 3), 120);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertAttack(recorder, "Attack|0|attacker=110|Minion:120",
                "能杀死且自身不死的有利交换必须解场。");
        }

        [Test]
        public void Attack_Unfavorable_GoesFace()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("ATK", 1, 1, 1), 110, summoningSick: false);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("WALL", 5, 5, 5), 120);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertAttack(recorder, "Attack|0|attacker=110|Hero:1",
                "无有利交换时必须打脸。");
        }

        [Test]
        public void Attack_Poisonous_TradesUpIntoBiggerMinion()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("POI", 1, 1, 1, Keyword.Poisonous), 110, summoningSick: false);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("BIG", 5, 5, 5), 120);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertAttack(recorder, "Attack|0|attacker=110|Minion:120",
                "剧毒随从应换任意敌方随从。");
        }

        [Test]
        public void Attack_StealthMinion_IsNeverTargeted()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("ATK", 5, 5, 5), 110, summoningSick: false);
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(1), MatchTestCards.Minion("SNEAK", 1, 1, 1, Keyword.Stealth), 120);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertAttack(recorder, "Attack|0|attacker=110|Hero:1",
                "敌方只有潜行随从时必须打脸。");
            Assert.That(recorder.Signatures.Any(s => s.Contains("Minion:120")), Is.False,
                "潜行随从永不成为目标（含技能与攻击）。");
        }

        [Test]
        public void Attack_SummoningSickness_Skipped()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("SICK", 3, 3, 3), 110, summoningSick: true);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertNoAttack(recorder);
        }

        [Test]
        public void Attack_Charge_IgnoresSummoningSickness()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("CHG", 2, 1, 3, Keyword.Charge), 110, summoningSick: true);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertAttack(recorder, "Attack|0|attacker=110|Hero:1",
                "冲锋随从登场回合即可攻击。");
        }

        [Test]
        public void Attack_Frozen_Skipped()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            CardInstance frozen = RuleEngineTestHelpers.AddToBoard(
                state.GetPlayer(0), MatchTestCards.Minion("FRZ", 3, 3, 3), 110, summoningSick: false);
            frozen.Statuses.Add(StatusFlags.Frozen);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            AssertNoAttack(recorder);
        }

        [Test]
        public void Attack_Windfury_AttacksTwice()
        {
            MatchState state = GreedyAiAgentFixtures.BuildState();
            RuleEngineTestHelpers.AddToBoard(state.GetPlayer(0), MatchTestCards.Minion("WF", 2, 1, 4, Keyword.Windfury), 110, summoningSick: false);

            RecordingAuthority recorder = GreedyAiAgentFixtures.RunSeatZeroTurn(
                state, GreedyAiAgentFixtures.BuildDatabase(), out _);

            var attacks = recorder.Signatures.Where(s => s.StartsWith("Attack|")).ToList();
            Assert.That(attacks.Count, Is.EqualTo(2), "风怒随从必须打满两次。");
            Assert.That(attacks[0], Is.EqualTo("Attack|0|attacker=110|Hero:1"));
            Assert.That(attacks[1], Is.EqualTo("Attack|0|attacker=110|Hero:1"));
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        private static void AssertAttack(RecordingAuthority recorder, string expected, string message)
        {
            var attacks = recorder.Signatures.Where(s => s.StartsWith("Attack|")).ToList();
            Assert.That(attacks.Count, Is.EqualTo(1), message + "（应恰好一次攻击）");
            Assert.That(attacks[0], Is.EqualTo(expected), message);
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }

        private static void AssertNoAttack(RecordingAuthority recorder)
        {
            Assert.That(recorder.Signatures.Any(s => s.StartsWith("Attack|")), Is.False,
                "无攻击资格时不应产生 AttackCommand。");
            Assert.That(recorder.InvalidCount, Is.EqualTo(0));
        }
    }
}
