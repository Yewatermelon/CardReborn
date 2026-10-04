using System;
using System.Collections.Generic;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T5：卡池查询服务（id/key 索引、筛选、明确错误）。</summary>
    public sealed class CardDatabaseTests
    {
        [Test]
        public void Ctor_WhenBundleValid_IndexesCardsAndCounts()
        {
            CardDatabase database = new CardDatabase(Bundle(
                Card(1, "NEUTRAL_PANGO"),
                Card(2, "MAGE_FIREBALL", CardClass.Mage, CardRarity.Rare),
                Card(3, "NEUTRAL_OLD", CardClass.Neutral, CardRarity.Common, enabled: false)));

            Assert.That(database.CardCount, Is.EqualTo(3));
            Assert.That(database.EnabledCardCount, Is.EqualTo(2));
            Assert.That(database.HeroCount, Is.EqualTo(1));
            Assert.That(database.AllCards.Count, Is.EqualTo(3));
        }

        [Test]
        public void TryGetCard_ByIdAndKey_ReturnsSameInstance()
        {
            CardDefinition expected = Card(5, "MAGE_BLOCK", CardClass.Mage);
            CardDatabase database = new CardDatabase(Bundle(expected));

            Assert.That(database.TryGetCard(5, out CardDefinition? byId), Is.True);
            Assert.That(byId, Is.SameAs(expected));
            Assert.That(database.TryGetCard("MAGE_BLOCK", out CardDefinition? byKey), Is.True);
            Assert.That(byKey, Is.SameAs(expected));
        }

        [Test]
        public void TryGetCard_WhenMissing_ReturnsFalseAndNull()
        {
            CardDatabase database = new CardDatabase(Bundle(Card(1, "NEUTRAL_PANGO")));

            Assert.That(database.TryGetCard(999, out CardDefinition? byId), Is.False);
            Assert.That(byId, Is.Null);
            Assert.That(database.TryGetCard("NOPE", out CardDefinition? byKey), Is.False);
            Assert.That(byKey, Is.Null);
        }

        [Test]
        public void RequireCard_WhenMissing_ThrowsWithIdentifier()
        {
            CardDatabase database = new CardDatabase(Bundle(Card(1, "NEUTRAL_PANGO")));

            ConfigLookupException byId = Assert.Throws<ConfigLookupException>(() => database.RequireCard(999))!;
            Assert.That(byId.Message, Does.Contain("999"));

            ConfigLookupException byKey = Assert.Throws<ConfigLookupException>(() => database.RequireCard("NOPE"))!;
            Assert.That(byKey.Message, Does.Contain("NOPE"));
        }

        [Test]
        public void RequireCard_WhenKeyIsNull_ThrowsArgumentNullException()
        {
            CardDatabase database = new CardDatabase(Bundle(Card(1, "NEUTRAL_PANGO")));

            Assert.Throws<ArgumentNullException>(() => database.RequireCard(null!));
        }

        [Test]
        public void FilterCards_WhenNoCriteria_ReturnsAllEnabled()
        {
            CardDatabase database = new CardDatabase(Bundle(
                Card(1, "A"),
                Card(2, "B", CardClass.Mage),
                Card(3, "C", enabled: false)));

            IReadOnlyList<CardDefinition> result = database.FilterCards();

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Key, Is.EqualTo("A"));
            Assert.That(result[1].Key, Is.EqualTo("B"));
        }

        [Test]
        public void FilterCards_ByClassRarityAndSetKey_CombinesCriteria()
        {
            CardDatabase database = new CardDatabase(Bundle(
                Card(1, "MAGE_COMMON_CORE", CardClass.Mage, CardRarity.Common, true, "Core"),
                Card(2, "MAGE_RARE_CORE", CardClass.Mage, CardRarity.Rare, true, "Core"),
                Card(3, "MAGE_RARE_EXP", CardClass.Mage, CardRarity.Rare, true, "Exp"),
                Card(4, "NEUTRAL_RARE_CORE", CardClass.Neutral, CardRarity.Rare, true, "Core")));

            Assert.That(database.FilterCards(cardClass: CardClass.Mage).Count, Is.EqualTo(3));
            Assert.That(database.FilterCards(rarity: CardRarity.Rare).Count, Is.EqualTo(3));
            Assert.That(database.FilterCards(setKey: "Exp").Count, Is.EqualTo(1));

            IReadOnlyList<CardDefinition> combined = database.FilterCards(
                cardClass: CardClass.Mage,
                rarity: CardRarity.Rare,
                setKey: "Core");

            Assert.That(combined.Count, Is.EqualTo(1));
            Assert.That(combined[0].Key, Is.EqualTo("MAGE_RARE_CORE"));
        }

        [Test]
        public void FilterCards_WhenIncludeDisabled_ReturnsParkedCardsToo()
        {
            CardDatabase database = new CardDatabase(Bundle(
                Card(1, "LIVE"),
                Card(2, "PARKED", enabled: false)));

            Assert.That(database.FilterCards().Count, Is.EqualTo(1));
            Assert.That(database.FilterCards(includeDisabled: true).Count, Is.EqualTo(2));
        }

        [Test]
        public void GetRarityWeight_WhenConfigured_ReturnsWeight()
        {
            CardDatabase database = new CardDatabase(Bundle(Card(1, "A")));

            Assert.That(database.GetRarityWeight(CardRarity.Common).Weight, Is.EqualTo(70));
            Assert.That(database.GetRarityWeight(CardRarity.Legendary).Rarity, Is.EqualTo(CardRarity.Legendary));
        }

        [Test]
        public void GetRarityWeight_WhenMissing_Throws()
        {
            ConfigBundle bundle = new ConfigBundle(
                new[] { Card(1, "A") },
                Heroes(),
                Powers(),
                new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100 } },
                Gacha(),
                Rules());
            CardDatabase database = new CardDatabase(bundle);

            ConfigLookupException exception =
                Assert.Throws<ConfigLookupException>(() => database.GetRarityWeight(CardRarity.Epic))!;

            Assert.That(exception.Message, Does.Contain("Epic"));
        }

        [Test]
        public void RequireHero_And_RequireHeroPower_Work()
        {
            CardDatabase database = new CardDatabase(Bundle(Card(1, "A")));

            Assert.That(database.RequireHero("HERO_MAGE").HeroPowerKey, Is.EqualTo("HERO_POWER_FIREBALL"));
            Assert.That(database.RequireHeroPower("HERO_POWER_FIREBALL").Cost, Is.EqualTo(2));
            Assert.Throws<ConfigLookupException>(() => database.RequireHero("HERO_NOPE"));
            Assert.Throws<ConfigLookupException>(() => database.RequireHeroPower("HERO_POWER_NOPE"));
        }

        [Test]
        public void Ctor_WhenDuplicateCardIdOrKey_Throws()
        {
            ConfigLookupException duplicateId = Assert.Throws<ConfigLookupException>(
                () => new CardDatabase(Bundle(Card(1, "A"), Card(1, "B"))))!;
            Assert.That(duplicateId.Message, Does.Contain("Id 重复"));

            ConfigLookupException duplicateKey = Assert.Throws<ConfigLookupException>(
                () => new CardDatabase(Bundle(Card(1, "A"), Card(2, "A"))))!;
            Assert.That(duplicateKey.Message, Does.Contain("Key 重复"));
        }

        [Test]
        public void Ctor_WhenBundleIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CardDatabase(null!));
        }

        [Test]
        public void GachaAndRules_AreExposedFromBundle()
        {
            CardDatabase database = new CardDatabase(Bundle(Card(1, "A")));

            Assert.That(database.Gacha.PackSize, Is.EqualTo(5));
            Assert.That(database.Rules.BoardLimit, Is.EqualTo(7));
        }

        private static CardDefinition Card(
            int id,
            string key,
            CardClass cardClass = CardClass.Neutral,
            CardRarity rarity = CardRarity.Common,
            bool enabled = true,
            string setKey = "Core")
        {
            return new CardDefinition
            {
                Id = id,
                Key = key,
                NameKey = key + "_NAME",
                DescKey = key + "_DESC",
                Cost = 1,
                Type = CardType.Minion,
                Rarity = rarity,
                Class = cardClass,
                Attack = 1,
                Health = 1,
                SetKey = setKey,
                Enabled = enabled
            };
        }

        private static IReadOnlyList<HeroDefinition> Heroes()
        {
            return new[]
            {
                new HeroDefinition
                {
                    Id = 1,
                    Key = "HERO_MAGE",
                    NameKey = "HERO_001_NAME",
                    Health = 30,
                    HeroPowerKey = "HERO_POWER_FIREBALL",
                    Class = CardClass.Mage
                }
            };
        }

        private static IReadOnlyList<HeroPowerDefinition> Powers()
        {
            return new[]
            {
                new HeroPowerDefinition
                {
                    Id = 1,
                    Key = "HERO_POWER_FIREBALL",
                    Cost = 2,
                    TargetRule = TargetRule.Any,
                    Effects = new[] { "DamageEffect:1" }
                }
            };
        }

        private static IReadOnlyList<RarityWeight> Weights()
        {
            return new[]
            {
                new RarityWeight { Rarity = CardRarity.Common, Weight = 70, MinPerPack = 0 },
                new RarityWeight { Rarity = CardRarity.Rare, Weight = 22, MinPerPack = 1 },
                new RarityWeight { Rarity = CardRarity.Epic, Weight = 6, MinPerPack = 0 },
                new RarityWeight { Rarity = CardRarity.Legendary, Weight = 2, MinPerPack = 0 }
            };
        }

        private static GachaConfig Gacha()
        {
            return new GachaConfig
            {
                PackSize = 5,
                CoinCost = 100,
                PityCount = 10,
                PityRarity = CardRarity.Legendary
            };
        }

        private static RulesConfig Rules()
        {
            return new RulesConfig { HeroHealth = 30, HandLimit = 10, BoardLimit = 7, ManaLimit = 10 };
        }

        private static ConfigBundle Bundle(params CardDefinition[] cards)
        {
            return new ConfigBundle(cards, Heroes(), Powers(), Weights(), Gacha(), Rules());
        }
    }
}
