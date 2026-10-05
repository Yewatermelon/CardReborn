using System;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T2：分区加入/移除、跨区移动与容量校验（原子性）。</summary>
    public sealed class ZoneMovementTests
    {
        [Test]
        public void Ctor_IsEmptyAndExposesTypeAndCapacity()
        {
            Zone zone = new Zone(ZoneType.Hand, capacity: 10);

            Assert.That(zone.Type, Is.EqualTo(ZoneType.Hand));
            Assert.That(zone.Capacity, Is.EqualTo(10));
            Assert.That(zone.Count, Is.EqualTo(0));
            Assert.That(zone.CanAdd(), Is.True);
            Assert.That(zone.Cards.Count, Is.EqualTo(0));
        }

        [Test]
        public void Ctor_WhenCapacityNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Zone(ZoneType.Hand, capacity: -1));
        }

        [Test]
        public void MoveIn_WhenArgumentsNull_Throws()
        {
            Zone hand = new Zone(ZoneType.Hand, 10);
            Zone deck = new Zone(ZoneType.Deck, null);

            Assert.Throws<ArgumentNullException>(() => hand.MoveIn(null!, from: deck));
            Assert.Throws<ArgumentNullException>(() => hand.MoveIn(
                CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0), from: null!));
        }

        [Test]
        public void Add_AppendsCardAndMarksCurrentZone()
        {
            Zone zone = new Zone(ZoneType.Deck, capacity: null);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);

            var result = zone.Add(card);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(zone.Count, Is.EqualTo(1));
            Assert.That(zone.Contains(card), Is.True);
            Assert.That(zone.Cards[0], Is.SameAs(card));
            Assert.That(card.CurrentZone, Is.EqualTo(ZoneType.Deck));
        }

        [Test]
        public void Add_WhenSameCardRepeated_ReturnsFailure()
        {
            Zone zone = new Zone(ZoneType.Hand, capacity: 10);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);
            zone.Add(card);

            var result = zone.Add(card);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_CARD_ALREADY_PRESENT"));
            Assert.That(zone.Count, Is.EqualTo(1));
        }

        [Test]
        public void Add_WhenAtCapacity_ReturnsZoneFull()
        {
            Zone zone = new Zone(ZoneType.Board, capacity: 2);

            zone.Add(CardInstance.FromDefinition(MatchTestCards.Minion("A"), 1, 0));
            zone.Add(CardInstance.FromDefinition(MatchTestCards.Minion("B"), 2, 0));
            var third = zone.Add(CardInstance.FromDefinition(MatchTestCards.Minion("C"), 3, 0));

            Assert.That(third.IsFailure, Is.True);
            Assert.That(third.ErrorCode, Is.EqualTo("ERROR_ZONE_FULL"));
            Assert.That(zone.Count, Is.EqualTo(2));
            Assert.That(zone.CanAdd(), Is.False);
        }

        [Test]
        public void Remove_TakesCardOutAndClearsCurrentZone()
        {
            Zone zone = new Zone(ZoneType.Hand, 10);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);
            zone.Add(card);

            bool removed = zone.Remove(card);

            Assert.That(removed, Is.True);
            Assert.That(zone.Count, Is.EqualTo(0));
            Assert.That(zone.Contains(card), Is.False);
            Assert.That(card.CurrentZone, Is.Null);
        }

        [Test]
        public void Remove_WhenCardForeign_ReturnsFalse()
        {
            Zone zone = new Zone(ZoneType.Graveyard, null);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);

            bool removed = zone.Remove(card);

            Assert.That(removed, Is.False);
            Assert.That(zone.Count, Is.EqualTo(0));
        }

        [Test]
        public void MoveIn_BetweenZones_RemovesFromSourceAndAddsToTarget()
        {
            Zone deck = new Zone(ZoneType.Deck, null);
            Zone hand = new Zone(ZoneType.Hand, 10);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);
            deck.Add(card);

            var result = hand.MoveIn(card, from: deck);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(deck.Count, Is.EqualTo(0));
            Assert.That(deck.Contains(card), Is.False);
            Assert.That(hand.Contains(card), Is.True);
            Assert.That(card.CurrentZone, Is.EqualTo(ZoneType.Hand));
        }

        [Test]
        public void MoveIn_WhenTargetFull_FailsAndKeepsCardInSource()
        {
            Zone board = new Zone(ZoneType.Board, capacity: 1);
            Zone hand = new Zone(ZoneType.Hand, capacity: 10);
            board.Add(CardInstance.FromDefinition(MatchTestCards.Minion("ONBOARD"), 9, 0));
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion("INHAND"), 1, 0);
            hand.Add(card);

            var result = board.MoveIn(card, from: hand);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_ZONE_FULL"));
            Assert.That(hand.Contains(card), Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(card.CurrentZone, Is.EqualTo(ZoneType.Hand));
        }

        [Test]
        public void MoveIn_WhenCardNotInSource_Fails()
        {
            Zone deck = new Zone(ZoneType.Deck, null);
            Zone hand = new Zone(ZoneType.Hand, 10);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);

            var result = hand.MoveIn(card, from: deck);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_CARD_NOT_IN_SOURCE"));
            Assert.That(hand.Count, Is.EqualTo(0));
            Assert.That(card.CurrentZone, Is.Null);
        }

        [Test]
        public void MoveIn_WhenSourceAndTargetSame_Fails()
        {
            Zone hand = new Zone(ZoneType.Hand, 10);
            CardInstance card = CardInstance.FromDefinition(MatchTestCards.Minion(), 1, 0);
            hand.Add(card);

            var result = hand.MoveIn(card, from: hand);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_SAME_ZONE"));
            Assert.That(hand.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnlimitedZone_AcceptsMoreThanStandardLimits()
        {
            Zone graveyard = new Zone(ZoneType.Graveyard, capacity: null);

            for (int i = 0; i < 12; i++)
            {
                var added = graveyard.Add(
                    CardInstance.FromDefinition(MatchTestCards.Minion("C" + i), i, 0));
                Assert.That(added.IsSuccess, Is.True);
            }

            Assert.That(graveyard.Count, Is.EqualTo(12));
        }

        [Test]
        public void PlayerState_Zones_CapacitiesComeFromRulesConfig()
        {
            RulesConfig rules = new RulesConfig { HandLimit = 10, BoardLimit = 7 };
            HeroState hero = new HeroState("MAGE", "POWER", 30);
            PlayerState player = new PlayerState(0, hero, new ManaPool(), rules);

            Assert.That(player.Deck.Type, Is.EqualTo(ZoneType.Deck));
            Assert.That(player.Deck.Capacity, Is.Null);
            Assert.That(player.Hand.Capacity, Is.EqualTo(10));
            Assert.That(player.Board.Capacity, Is.EqualTo(7));
            Assert.That(player.Graveyard.Capacity, Is.Null);
        }

        [Test]
        public void PlayerState_WithoutRules_UsesConfigDefaults()
        {
            HeroState hero = new HeroState("MAGE", "POWER", 30);
            PlayerState player = new PlayerState(0, hero, new ManaPool());

            Assert.That(player.Hand.Capacity, Is.EqualTo(10));
            Assert.That(player.Board.Capacity, Is.EqualTo(7));
        }
    }
}
