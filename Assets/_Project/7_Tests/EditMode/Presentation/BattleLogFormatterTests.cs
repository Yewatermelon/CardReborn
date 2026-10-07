using System;
using Card.Domain.Match;
using Card.Presentation.Battle.Log;
using NUnit.Framework;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>M5-T7：12 类事件与日志行一一对应；未知类型显式报错。</summary>
    [TestFixture]
    public sealed class BattleLogFormatterTests
    {
        [Test]
        public void PhaseChanged_FormatsBothPhases()
        {
            string line = BattleLogFormatter.Format(
                new PhaseChangedEvent(TurnPhase.Main, TurnPhase.TurnEnd));

            StringAssert.Contains("Main", line);
            StringAssert.Contains("TurnEnd", line);
        }

        [Test]
        public void TurnStarted_FormatsTurnNumberAndSeat()
        {
            string line = BattleLogFormatter.Format(new TurnStartedEvent(3, 1));

            StringAssert.Contains("3", line);
            StringAssert.Contains("1", line);
        }

        [Test]
        public void TurnEnded_FormatsSeat()
        {
            string line = BattleLogFormatter.Format(new TurnEndedEvent(4, 0));

            StringAssert.Contains("0", line);
        }

        [Test]
        public void CardDrawn_FormatsSeatAndCard()
        {
            string line = BattleLogFormatter.Format(new CardDrawnEvent(0, 42));

            StringAssert.Contains("0", line);
            StringAssert.Contains("42", line);
        }

        [Test]
        public void CardBurned_FormatsBurn()
        {
            string line = BattleLogFormatter.Format(new CardBurnedEvent(1, 7));

            StringAssert.Contains("1", line);
            StringAssert.Contains("7", line);
        }

        [Test]
        public void Fatigue_FormatsDamageAndCounter()
        {
            string line = BattleLogFormatter.Format(new FatigueEvent(1, 3, 3));

            StringAssert.Contains("3", line);
        }

        [Test]
        public void Damage_OnMinion_FormatsTargetAndAmount()
        {
            string line = BattleLogFormatter.Format(
                new DamageEvent(5, 9, null, 4, divineShieldConsumed: false));

            StringAssert.Contains("9", line);
            StringAssert.Contains("4", line);
        }

        [Test]
        public void Damage_OnHero_FormatsHeroSeat()
        {
            string line = BattleLogFormatter.Format(
                new DamageEvent(5, null, 1, 6, divineShieldConsumed: false));

            StringAssert.Contains("1", line);
            StringAssert.Contains("6", line);
        }

        [Test]
        public void Damage_DivineShieldConsumed_AppendsShieldNote()
        {
            string line = BattleLogFormatter.Format(
                new DamageEvent(null, 9, null, 2, divineShieldConsumed: true));

            StringAssert.Contains("9", line);
            StringAssert.Contains("2", line);
        }

        [Test]
        public void Healing_OnMinion_FormatsAmount()
        {
            string line = BattleLogFormatter.Format(new HealingEvent(11, null, 5));

            StringAssert.Contains("11", line);
            StringAssert.Contains("5", line);
        }

        [Test]
        public void CardDeath_FormatsInstanceId()
        {
            string line = BattleLogFormatter.Format(new CardDeathEvent(13));

            StringAssert.Contains("13", line);
        }

        [Test]
        public void CardPlayed_FormatsSeatAndCard()
        {
            string line = BattleLogFormatter.Format(new CardPlayedEvent(0, 21));

            StringAssert.Contains("0", line);
            StringAssert.Contains("21", line);
        }

        [Test]
        public void AttackDeclared_OnHero_FormatsAttackerAndHero()
        {
            string line = BattleLogFormatter.Format(new AttackDeclaredEvent(8, null, 1));

            StringAssert.Contains("8", line);
            StringAssert.Contains("1", line);
        }

        [Test]
        public void AttackDeclared_OnMinion_FormatsBoth()
        {
            string line = BattleLogFormatter.Format(new AttackDeclaredEvent(8, 12, null));

            StringAssert.Contains("8", line);
            StringAssert.Contains("12", line);
        }

        [Test]
        public void MatchEnded_Win_FormatsWinnerAndReason()
        {
            string line = BattleLogFormatter.Format(
                new MatchEndedEvent(MatchResult.Player0Wins, 0, 9, "英雄生命归零"));

            StringAssert.Contains("0", line);
            StringAssert.Contains("9", line);
        }

        [Test]
        public void MatchEnded_Draw_FormatsDraw()
        {
            string line = BattleLogFormatter.Format(
                new MatchEndedEvent(MatchResult.Draw, null, 9, "双方同时归零"));

            StringAssert.Contains("9", line);
        }

        private sealed class UnknownEvent : GameEvent
        {
        }

        [Test]
        public void UnknownEvent_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleLogFormatter.Format(new UnknownEvent()));
        }

        [Test]
        public void AllTwelveTypes_ProduceDistinctLines()
        {
            var lines = new[]
            {
                BattleLogFormatter.Format(new PhaseChangedEvent(TurnPhase.Draw, TurnPhase.Main)),
                BattleLogFormatter.Format(new TurnStartedEvent(1, 0)),
                BattleLogFormatter.Format(new TurnEndedEvent(1, 0)),
                BattleLogFormatter.Format(new CardDrawnEvent(0, 1)),
                BattleLogFormatter.Format(new CardBurnedEvent(0, 2)),
                BattleLogFormatter.Format(new FatigueEvent(0, 1, 1)),
                BattleLogFormatter.Format(new DamageEvent(1, 2, null, 3, false)),
                BattleLogFormatter.Format(new HealingEvent(2, null, 3)),
                BattleLogFormatter.Format(new CardDeathEvent(2)),
                BattleLogFormatter.Format(new CardPlayedEvent(0, 3)),
                BattleLogFormatter.Format(new AttackDeclaredEvent(4, 5, null)),
                BattleLogFormatter.Format(new MatchEndedEvent(MatchResult.Player1Wins, 1, 7, "x")),
            };

            Assert.That(lines.Length, Is.EqualTo(12));
            foreach (string line in lines)
            {
                Assert.That(line, Is.Not.Null.And.Not.Empty);
            }
        }
    }
}
