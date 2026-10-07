using NUnit.Framework;
using Card.Domain.Config;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class CardViewDataTests
    {
        [Test]
        public void Constructor_AssignsAllFields()
        {
            var data = new CardViewData(
                name: "Test Card",
                description: "A test description",
                cost: 3,
                attack: 2,
                health: 4,
                artKey: "art_test_001",
                type: CardType.Minion);

            Assert.That(data.Name, Is.EqualTo("Test Card"));
            Assert.That(data.Description, Is.EqualTo("A test description"));
            Assert.That(data.Cost, Is.EqualTo(3));
            Assert.That(data.Attack, Is.EqualTo(2));
            Assert.That(data.Health, Is.EqualTo(4));
            Assert.That(data.ArtKey, Is.EqualTo("art_test_001"));
            Assert.That(data.Type, Is.EqualTo(CardType.Minion));
        }

        [Test]
        public void Constructor_WithNullName_FallsBackToEmpty()
        {
            var data = new CardViewData(
                name: null!,
                description: "desc",
                cost: 0,
                attack: 0,
                health: 0,
                artKey: null!,
                type: CardType.Spell);

            Assert.That(data.Name, Is.Empty);
            Assert.That(data.ArtKey, Is.Empty);
        }

        [Test]
        public void FromDefinition_MapsAllFields()
        {
            var def = new CardDefinition
            {
                NameKey = "CARD_001_NAME",
                DescKey = "CARD_001_DESC",
                Cost = 2,
                Attack = 2,
                Health = 3,
                ArtKey = "art_card_001",
                Type = CardType.Minion
            };

            CardViewData data = CardViewData.FromDefinition(def);

            Assert.That(data.Name, Is.EqualTo(def.NameKey));
            Assert.That(data.Description, Is.EqualTo(def.DescKey));
            Assert.That(data.Cost, Is.EqualTo(def.Cost));
            Assert.That(data.Attack, Is.EqualTo(def.Attack));
            Assert.That(data.Health, Is.EqualTo(def.Health));
            Assert.That(data.ArtKey, Is.EqualTo(def.ArtKey));
            Assert.That(data.Type, Is.EqualTo(def.Type));
        }

        [Test]
        public void FromDefinition_WithNullDefinition_ThrowsArgumentNullException()
        {
            Assert.That(
                () => CardViewData.FromDefinition(null!),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("definition"));
        }

        [Test]
        public void FromDefinition_SpellType_StillMapsFields()
        {
            var def = new CardDefinition
            {
                NameKey = "SPELL_001",
                Cost = 1,
                Type = CardType.Spell
            };

            CardViewData data = CardViewData.FromDefinition(def);

            Assert.That(data.Type, Is.EqualTo(CardType.Spell));
            Assert.That(data.Attack, Is.EqualTo(0));
            Assert.That(data.Health, Is.EqualTo(0));
        }
    }
}
