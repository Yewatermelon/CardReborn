using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T5：开局初始化（牌库/洗牌/先后手/起手/幸运币），全部基于真实源表。</summary>
    public sealed class MatchFactoryTests
    {
        private const string MageHero = "HERO_MAGE";
        private const string WarriorHero = "HERO_WARRIOR";
        private const string CoinKey = "NEUTRAL_THE_COIN";

        private CardDatabase _database = null!;
        private List<string> _originalDeck = null!;

        [SetUp]
        public void Setup()
        {
            _database = BuildDatabase();
            _originalDeck = LoadEnabledDeckKeys();
        }

        [Test]
        public void Create_FirstPlayerKeepsThreeCards_DeckHas27()
        {
            MatchState match = NewMatch(seed: 1);

            Assert.That(match.ActivePlayer.Hand.Count, Is.EqualTo(3));
            Assert.That(match.ActivePlayer.Deck.Count, Is.EqualTo(27));
        }

        [Test]
        public void Create_SecondPlayerHasFourCardsAndCoin()
        {
            MatchState match = NewMatch(seed: 1);
            PlayerState second = match.GetPlayer(1 - match.ActivePlayerId);

            Assert.That(second.Hand.Count, Is.EqualTo(5));
            Assert.That(second.Deck.Count, Is.EqualTo(26));
        }

        [Test]
        public void Create_FirstTurnSkipsDraw_ForManySeeds()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                MatchState match = NewMatch(seed);
                Assert.That(match.ActivePlayer.Hand.Count, Is.EqualTo(3), "seed=" + seed);
            }
        }

        [Test]
        public void Create_GivesExactlyOneCoinToSecondPlayerOnly()
        {
            MatchState match = NewMatch(seed: 7);
            PlayerState second = match.GetPlayer(1 - match.ActivePlayerId);

            Assert.That(CountInHand(second, CoinKey), Is.EqualTo(1));
            Assert.That(FindInHand(second, CoinKey).CurrentZone, Is.EqualTo(ZoneType.Hand));
            Assert.That(CountInHand(match.ActivePlayer, CoinKey), Is.EqualTo(0));
        }

        [Test]
        public void Create_SameSeedProducesIdenticalDeckOrderAndFirstSeat()
        {
            MatchState a = NewMatch(123);
            MatchState b = NewMatch(123);

            Assert.That(DeckKeys(a, 0), Is.EqualTo(DeckKeys(b, 0)));
            Assert.That(DeckKeys(a, 1), Is.EqualTo(DeckKeys(b, 1)));
            Assert.That(a.ActivePlayerId, Is.EqualTo(b.ActivePlayerId));
        }

        [Test]
        public void Create_ShuffleActuallyReordersDeck()
        {
            bool reordered = false;
            for (int seed = 1; seed <= 20; seed++)
            {
                MatchState match = NewMatch(seed);
                if (!MatchesOriginalPrefix(match.GetPlayer(0)))
                {
                    reordered = true;
                    break;
                }
            }

            Assert.That(reordered, Is.True, "多个种子下牌序都与原序相同，洗牌可能是空操作。");
        }

        [Test]
        public void Create_FirstSeatIsRandomizedAcrossSeeds()
        {
            HashSet<int> firstSeats = new HashSet<int>();
            for (int seed = 1; seed <= 100; seed++)
            {
                firstSeats.Add(NewMatch(seed).ActivePlayerId);
            }

            Assert.That(firstSeats, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void Create_FirstTurnState_MainTurnOne_ActiveHasOneMana()
        {
            MatchState match = NewMatch(seed: 42);
            PlayerState second = match.GetPlayer(1 - match.ActivePlayerId);

            Assert.That(match.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(match.TurnNumber, Is.EqualTo(1));
            Assert.That(match.ActivePlayer.Mana.Max, Is.EqualTo(1));
            Assert.That(match.ActivePlayer.Mana.Current, Is.EqualTo(1));
            Assert.That(second.Mana.Max, Is.EqualTo(0));
            Assert.That(second.Mana.Current, Is.EqualTo(0));
        }

        [Test]
        public void Create_InstanceIdsUnique_61Cards_WithOwnershipAndZone()
        {
            MatchState match = NewMatch(seed: 99);
            HashSet<int> ids = new HashSet<int>();

            foreach (PlayerState player in match.Players)
            {
                foreach (Zone zone in new[] { player.Deck, player.Hand, player.Board, player.Graveyard })
                {
                    foreach (CardInstance card in zone.Cards)
                    {
                        Assert.That(ids.Add(card.InstanceId), Is.True, "InstanceId 重复：" + card.InstanceId);
                        Assert.That(card.OwnerId, Is.EqualTo(player.Id));
                        Assert.That(card.CurrentZone, Is.EqualTo(zone.Type));
                    }
                }
            }

            Assert.That(ids.Count, Is.EqualTo(61));
        }

        [Test]
        public void Create_HeroPowerKeyWiredFromHeroDefinition()
        {
            MatchState match = NewMatch(seed: 5);

            Assert.That(match.GetPlayer(0).Hero.HeroPowerKey, Is.EqualTo("HERO_POWER_FIREBALL"));
            Assert.That(match.GetPlayer(1).Hero.HeroPowerKey, Is.EqualTo("HERO_POWER_BASH"));
        }

        [Test]
        public void Create_DeckWithWrongSize_Throws()
        {
            MatchSetupRequest bad = new MatchSetupRequest(MageHero, _originalDeck.Take(29).ToList());

            Assert.Throws<System.ArgumentException>(
                () => MatchFactory.Create(_database, bad, Seat1(), new SeededRandomProvider(1)));
        }

        [Test]
        public void Create_UnknownCardKey_Throws()
        {
            List<string> keys = new List<string>(_originalDeck) { [5] = "NO_SUCH_CARD" };
            MatchSetupRequest bad = new MatchSetupRequest(MageHero, keys);

            Assert.Throws<ConfigLookupException>(
                () => MatchFactory.Create(_database, bad, Seat1(), new SeededRandomProvider(1)));
        }

        [Test]
        public void Create_UnknownHeroKey_Throws()
        {
            MatchSetupRequest bad = new MatchSetupRequest("NO_SUCH_HERO", _originalDeck);

            Assert.Throws<ConfigLookupException>(
                () => MatchFactory.Create(_database, bad, Seat1(), new SeededRandomProvider(1)));
        }

        [Test]
        public void Create_NullArguments_Throw()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MatchFactory.Create(null!, Seat0(), Seat1(), new SeededRandomProvider(1)));
            Assert.Throws<System.ArgumentNullException>(
                () => MatchFactory.Create(_database, null!, Seat1(), new SeededRandomProvider(1)));
            Assert.Throws<System.ArgumentNullException>(
                () => MatchFactory.Create(_database, Seat0(), null!, new SeededRandomProvider(1)));
            Assert.Throws<System.ArgumentNullException>(
                () => MatchFactory.Create(_database, Seat0(), Seat1(), null!));
        }

        private MatchState NewMatch(int seed)
        {
            return MatchFactory.Create(
                _database, Seat0(), Seat1(), new SeededRandomProvider(seed));
        }

        private MatchSetupRequest Seat0() => new MatchSetupRequest(MageHero, _originalDeck);

        private MatchSetupRequest Seat1() => new MatchSetupRequest(WarriorHero, _originalDeck);

        private bool MatchesOriginalPrefix(PlayerState player)
        {
            List<string> deck = DeckKeysOf(player);
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] != _originalDeck[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> DeckKeys(MatchState match, int seatId)
        {
            return DeckKeysOf(match.GetPlayer(seatId));
        }

        private static List<string> DeckKeysOf(PlayerState player)
        {
            return player.Deck.Cards.Select(card => card.CardKey).ToList();
        }

        private static int CountInHand(PlayerState player, string cardKey)
        {
            return player.Hand.Cards.Count(card => card.CardKey == cardKey);
        }

        private static CardInstance FindInHand(PlayerState player, string cardKey)
        {
            return player.Hand.Cards.First(card => card.CardKey == cardKey);
        }

        private static List<string> LoadEnabledDeckKeys()
        {
            CsvTable cards = ConfigTemplates.Load(ConfigTemplates.CardsFile);
            List<string> keys = new List<string>();
            for (int row = 0; row < cards.RowCount && keys.Count < 30; row++)
            {
                if (cards.GetCell(row, "Enabled") == "TRUE")
                {
                    keys.Add(cards.GetCell(row, "Key"));
                }
            }

            return keys;
        }

        private static CardDatabase BuildDatabase()
        {
            ConfigSourceSet sources = new ConfigSourceSet(
                ConfigTemplates.Load(ConfigTemplates.CardsFile),
                ConfigTemplates.Load(ConfigTemplates.HeroesFile),
                ConfigTemplates.Load(ConfigTemplates.HeroPowersFile),
                ConfigTemplates.Load(ConfigTemplates.RarityWeightsFile),
                ConfigTemplates.Load(ConfigTemplates.GachaConfigFile),
                ConfigTemplates.Load(ConfigTemplates.RulesFile));
            ConfigValidationReport report = ConfigValidator.Validate(sources);

            Assert.That(report.HasErrors, Is.False, report.ToText());
            return new CardDatabase(report.Result!);
        }
    }
}
