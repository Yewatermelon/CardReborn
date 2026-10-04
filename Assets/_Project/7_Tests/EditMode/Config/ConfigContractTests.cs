using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T1：枚举列严格解析、契约默认值与文件信封。</summary>
    public sealed class ConfigContractTests
    {
        [Test]
        public void TryParseEnum_WhenValidName_Parses()
        {
            Assert.That(ConfigTokens.TryParseEnum("Spell", out CardType type), Is.True);
            Assert.That(type, Is.EqualTo(CardType.Spell));

            Assert.That(ConfigTokens.TryParseEnum("legendary", out CardRarity rarity), Is.True);
            Assert.That(rarity, Is.EqualTo(CardRarity.Legendary));
        }

        [Test]
        public void TryParseEnum_WhenWhitespacePadded_Parses()
        {
            Assert.That(ConfigTokens.TryParseEnum("  Rare  ", out CardRarity rarity), Is.True);
            Assert.That(rarity, Is.EqualTo(CardRarity.Rare));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TryParseEnum_WhenEmpty_Fails(string? text)
        {
            Assert.That(ConfigTokens.TryParseEnum(text, out CardType type), Is.False);
            Assert.That(type, Is.EqualTo(default(CardType)));
        }

        [Test]
        public void TryParseEnum_WhenUndefinedName_Fails()
        {
            Assert.That(ConfigTokens.TryParseEnum("Dragon", out CardClass cardClass), Is.False);
            Assert.That(ConfigTokens.TryParseEnum("Middle", out TargetRule target), Is.False);
        }

        [Test]
        public void TryParseEnum_WhenNumeric_Fails()
        {
            Assert.That(
                ConfigTokens.TryParseEnum("1", out CardType type),
                Is.False,
                "配置表禁止用数字代替枚举名");
            Assert.That(type, Is.EqualTo(default(CardType)));
        }

        [Test]
        public void CardDefinition_WhenDefaulted_HasSafeDefaults()
        {
            CardDefinition card = new CardDefinition();

            Assert.That(card.Key, Is.Empty);
            Assert.That(card.NameKey, Is.Empty);
            Assert.That(card.DescKey, Is.Empty);
            Assert.That(card.SetKey, Is.Empty);
            Assert.That(card.ArtKey, Is.Empty);
            Assert.That(card.AudioKey, Is.Empty);
            Assert.That(card.Effects, Is.Not.Null.And.Empty);
            Assert.That(card.Keywords, Is.EqualTo(Keyword.None));
            Assert.That(card.Type, Is.EqualTo(CardType.Minion));
            Assert.That(card.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(card.Class, Is.EqualTo(CardClass.Neutral));
            Assert.That(card.TargetRule, Is.EqualTo(TargetRule.None));
            Assert.That(card.Enabled, Is.True, "默认启用；废弃卡才置 false");
        }

        [Test]
        public void CardDefinition_WhenInitialized_KeepsValues()
        {
            CardDefinition card = new CardDefinition
            {
                Id = 5,
                Key = "MAGE_FIREBALL",
                Cost = 4,
                Type = CardType.Spell,
                Class = CardClass.Mage,
                TargetRule = TargetRule.EnemyMinion,
                Effects = new[] { "DamageEffect:6" }
            };

            Assert.That(card.Id, Is.EqualTo(5));
            Assert.That(card.Key, Is.EqualTo("MAGE_FIREBALL"));
            Assert.That(card.Cost, Is.EqualTo(4));
            Assert.That(card.Type, Is.EqualTo(CardType.Spell));
            Assert.That(card.Class, Is.EqualTo(CardClass.Mage));
            Assert.That(card.TargetRule, Is.EqualTo(TargetRule.EnemyMinion));
            Assert.That(card.Effects, Is.EqualTo(new[] { "DamageEffect:6" }));
        }

        [Test]
        public void HeroDefinition_WhenDefaulted_UsesThirtyHealth()
        {
            HeroDefinition hero = new HeroDefinition();

            Assert.That(hero.Health, Is.EqualTo(30));
            Assert.That(hero.Class, Is.EqualTo(CardClass.Neutral));
            Assert.That(hero.HeroPowerKey, Is.Empty);
        }

        [Test]
        public void HeroPowerDefinition_WhenDefaulted_MatchesPrd()
        {
            HeroPowerDefinition power = new HeroPowerDefinition();

            Assert.That(power.Cost, Is.EqualTo(2), "默认英雄技能 2 费（Docs/01 第 3.6 节）");
            Assert.That(power.TargetRule, Is.EqualTo(TargetRule.Any));
            Assert.That(power.Effects, Is.Empty);
        }

        [Test]
        public void RulesConfig_WhenDefaulted_MatchesPrd()
        {
            RulesConfig rules = new RulesConfig();

            Assert.That(rules.HeroHealth, Is.EqualTo(30));
            Assert.That(rules.HandLimit, Is.EqualTo(10));
            Assert.That(rules.BoardLimit, Is.EqualTo(7));
            Assert.That(rules.ManaLimit, Is.EqualTo(10));
        }

        [Test]
        public void GachaConfig_WhenDefaulted_MatchesPrd()
        {
            GachaConfig gacha = new GachaConfig();

            Assert.That(gacha.PackSize, Is.EqualTo(5), "每包 5 张");
            Assert.That(gacha.CoinCost, Is.EqualTo(100));
            Assert.That(gacha.PityCount, Is.EqualTo(10), "默认 10 包保底");
            Assert.That(gacha.PityRarity, Is.EqualTo(CardRarity.Legendary));
        }

        [Test]
        public void RarityWeight_WhenDefaulted_IsCommonWithZeroWeight()
        {
            RarityWeight weight = new RarityWeight();

            Assert.That(weight.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(weight.Weight, Is.EqualTo(0));
            Assert.That(weight.MinPerPack, Is.EqualTo(0));
        }

        [Test]
        public void ConfigDocument_WhenDefaulted_HasCurrentVersionAndEmptyEntries()
        {
            ConfigDocument<CardDefinition> document = new ConfigDocument<CardDefinition>();

            Assert.That(document.SchemaVersion, Is.EqualTo(ConfigSchema.CurrentVersion));
            Assert.That(document.Entries, Is.Not.Null.And.Empty);
        }

        [Test]
        public void ConfigDocument_WhenInitialized_KeepsEntries()
        {
            ConfigDocument<CardDefinition> document = new ConfigDocument<CardDefinition>
            {
                Entries = new[] { new CardDefinition { Id = 1, Key = "NEUTRAL_PANGO" } }
            };

            Assert.That(document.Entries.Count, Is.EqualTo(1));
            Assert.That(document.Entries[0].Key, Is.EqualTo("NEUTRAL_PANGO"));
        }

        [Test]
        public void ConfigSchema_ExposesFileNamesAndVersion()
        {
            Assert.That(ConfigSchema.CurrentVersion, Is.GreaterThan(0));
            Assert.That(ConfigSchema.VersionProperty, Is.EqualTo("schemaVersion"));
            Assert.That(ConfigSchema.CardsFileName, Does.EndWith(".json"));
            Assert.That(ConfigSchema.HeroesFileName, Does.EndWith(".json"));
            Assert.That(ConfigSchema.HeroPowersFileName, Does.EndWith(".json"));
            Assert.That(ConfigSchema.GachaFileName, Does.EndWith(".json"));
            Assert.That(ConfigSchema.RulesFileName, Does.EndWith(".json"));
        }
    }
}
