using System;
using System.Collections.Generic;
using NUnit.Framework;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T9 失败测试（红）：MatchStateSerializer 尚未实现，应先编译失败。</summary>
    [TestFixture]
    public sealed class MatchStateSerializerTests
    {
        private static PlayerState CreatePlayer(int id, string heroKey = "HERO_A", string powerKey = "POWER_A")
        {
            return new PlayerState(id, new HeroState(heroKey, powerKey, 30), new ManaPool(max: 3, current: 2));
        }

        private static CardInstance NewCard(string key, int instanceId, int ownerId, Keyword keywords = Keyword.None)
        {
            return CardInstance.FromDefinition(MatchTestCards.Minion(key, keywords: keywords), instanceId, ownerId);
        }

        private static MatchState BuildRichState()
        {
            PlayerState p0 = CreatePlayer(0);
            PlayerState p1 = CreatePlayer(1, "HERO_B", "POWER_B");

            p0.Deck.Add(NewCard("CARD_D0", 0, 0));
            p0.Hand.Add(NewCard("CARD_H0", 1, 0));
            CardInstance taunt = NewCard("CARD_TAUNT", 2, 0, Keyword.Taunt | Keyword.Charge);
            taunt.Statuses.Add(StatusFlags.Frozen);
            taunt.AttacksUsedThisTurn = 1;
            p0.Board.Add(taunt);
            p0.Graveyard.Add(NewCard("CARD_G0", 3, 0));
            p0.Hero.Armor = 4;
            p0.FatigueCounter = 2;

            p1.Board.Add(NewCard("CARD_B0", 10, 1, Keyword.DivineShield));
            p1.Board.Cards[0].Statuses.Add(StatusFlags.SummoningSickness);

            MatchState state = new MatchState(p0, p1, activePlayerId: 0)
            {
                Phase = TurnPhase.Main,
                TurnNumber = 7
            };
            return state;
        }

        [Test]
        public void RoundTrip_RichState_RestoresSemanticallyEqual()
        {
            MatchState state = BuildRichState();

            string json = Card.Application.Match.MatchStateSerializer.Serialize(state);
            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(json);

            MatchStateComparer.AssertEqual(state, restored);
        }

        [Test]
        public void RoundTrip_PreservesZoneOrder()
        {
            MatchState state = new MatchState(CreatePlayer(0), CreatePlayer(1, "HERO_B", "POWER_B"), 0);
            state.Phase = TurnPhase.Main;
            state.GetPlayer(0).Deck.Add(NewCard("A", 0, 0));
            state.GetPlayer(0).Deck.Add(NewCard("B", 1, 0));
            state.GetPlayer(0).Deck.Add(NewCard("C", 2, 0));

            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(
                Card.Application.Match.MatchStateSerializer.Serialize(state));

            Zone deck = restored.GetPlayer(0).Deck;
            Assert.That(deck.Cards[0].CardKey, Is.EqualTo("A"));
            Assert.That(deck.Cards[1].CardKey, Is.EqualTo("B"));
            Assert.That(deck.Cards[2].CardKey, Is.EqualTo("C"));
        }

        [Test]
        public void Serialize_SameStateTwice_ProducesIdenticalText()
        {
            MatchState state = BuildRichState();

            string json1 = Card.Application.Match.MatchStateSerializer.Serialize(state);
            string json2 = Card.Application.Match.MatchStateSerializer.Serialize(state);

            Assert.That(json2, Is.EqualTo(json1));
        }

        [Test]
        public void RoundTrip_EmptyZones_Succeeds()
        {
            MatchState state = new MatchState(CreatePlayer(0), CreatePlayer(1, "HERO_B", "POWER_B"), 0);
            state.Phase = TurnPhase.MatchStart;

            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(
                Card.Application.Match.MatchStateSerializer.Serialize(state));

            MatchStateComparer.AssertEqual(state, restored);
        }

 [Test]
        public void Serialize_CardJsonOmitsRedundantZone_RestoreAssignsContainingZone()
        {
            MatchState state = new MatchState(CreatePlayer(0), CreatePlayer(1, "HERO_B", "POWER_B"), 0);
            state.Phase = TurnPhase.Main;
            state.GetPlayer(0).Graveyard.Add(NewCard("DEAD_CARD", 5, 0));

            string json = Card.Application.Match.MatchStateSerializer.Serialize(state);
            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(json);

            Assert.That(json, Does.Not.Contain("currentZone"));
            CardInstance card = restored.GetPlayer(0).Graveyard.Cards[0];
            Assert.That(card.CurrentZone, Is.EqualTo(ZoneType.Graveyard));
        }

        [Test]
        public void RoundTrip_FinishedMatchWithZeroHealth_PreservesTerminalState()
        {
            MatchState state = new MatchState(CreatePlayer(0), CreatePlayer(1, "HERO_B", "POWER_B"), 0);
            state.Phase = TurnPhase.MatchEnd;
            state.IsFinished = true;
            state.GetPlayer(1).Hero.Health = 0;

            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(
                Card.Application.Match.MatchStateSerializer.Serialize(state));

            MatchStateComparer.AssertEqual(state, restored);
        }

        [Test]
        public void RoundTrip_FactoryMatch_RuleEngineGivesSameResult()
        {
            CardDatabase db = BuildFactoryDatabase();
            List<string> deckKeys = new List<string>();
            for (int i = 0; i < 30; i++) deckKeys.Add("DECK_CARD");
            MatchSetupRequest req = new MatchSetupRequest("HERO_A", deckKeys);
            MatchState state = MatchFactory.Create(
                db, req, req, new SeededRandomProvider(seed: 123));

            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(
                Card.Application.Match.MatchStateSerializer.Serialize(state));

            MatchStateComparer.AssertEqual(state, restored);
            PlayCardCommand cmd = new PlayCardCommand(
                state.ActivePlayerId, state.ActivePlayer.Hand.Cards[0].InstanceId, TargetRef.None);
            Assert.That(RuleEngine.Validate(restored, cmd, db).IsValid,
                Is.EqualTo(RuleEngine.Validate(state, cmd, db).IsValid));
        }

        [Test]
        public void Deserialize_InvalidJson_Throws()
        {
            Assert.Throws<FormatException>(
                () => Card.Application.Match.MatchStateSerializer.Deserialize("{ not json"));
        }

        [Test]
        public void Deserialize_UnsupportedVersion_Throws()
        {
            string json = "{\"version\": 999, \"players\": []}";

            try
            {
                Card.Application.Match.MatchStateSerializer.Deserialize(json);
                Assert.Fail("应抛 NotSupportedException。");
            }
            catch (NotSupportedException ex)
            {
                Assert.That(ex.Message, Does.Contain("999"));
            }
        }

        [Test]
        public void RoundTrip_ProducesIndependentObjectGraph()
        {
            MatchState state = BuildRichState();

            MatchState restored = Card.Application.Match.MatchStateSerializer.Deserialize(
                Card.Application.Match.MatchStateSerializer.Serialize(state));

            restored.GetPlayer(0).Hero.Health = 1;
            restored.GetPlayer(0).Board.Cards[0].Health = 99;

            Assert.That(state.GetPlayer(0).Hero.Health, Is.EqualTo(30));
            Assert.That(state.GetPlayer(0).Board.Cards[0].Health, Is.EqualTo(2));
        }

        private static CardDatabase BuildFactoryDatabase()
        {
            ConfigBundle bundle = new ConfigBundle(
                cards: new[]
                {
                    MatchTestCards.Minion("DECK_CARD"),
                    new CardDefinition { Id = 9900, Key = "COIN_CARD", Cost = 0, Type = CardType.Spell }
                },
                heroes: new[]
                {
                    new HeroDefinition { Id = 1, Key = "HERO_A", Health = 30, HeroPowerKey = "POWER_A" }
                },
                heroPowers: new[]
                {
                    new HeroPowerDefinition { Id = 1, Key = "POWER_A", Cost = 2 }
                },
                rarityWeights: Array.Empty<RarityWeight>(),
                gacha: new GachaConfig(),
                rules: new RulesConfig { TheCoinCardKey = "COIN_CARD" });
            return new CardDatabase(bundle);
        }
    }
}
