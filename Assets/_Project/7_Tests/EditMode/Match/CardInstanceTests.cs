using System;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T2：CardInstance 从配置定义创建的语义。</summary>
    public sealed class CardInstanceTests
    {
        [Test]
        public void FromDefinition_Minion_CopiesIdentityAndStats()
        {
            CardDefinition definition = MatchTestCards.Minion("CHILLWIND_YETI", attack: 4, health: 5, cost: 4);

            CardInstance card = CardInstance.FromDefinition(definition, instanceId: 7, ownerId: 1);

            Assert.That(card.InstanceId, Is.EqualTo(7));
            Assert.That(card.CardKey, Is.EqualTo("CHILLWIND_YETI"));
            Assert.That(card.OwnerId, Is.EqualTo(1));
            Assert.That(card.Attack, Is.EqualTo(4));
            Assert.That(card.MaxHealth, Is.EqualTo(5));
            Assert.That(card.Health, Is.EqualTo(5));
            Assert.That(card.CurrentZone, Is.Null);
        }

        [Test]
        public void FromDefinition_Spell_HasZeroCombatStats()
        {
            CardDefinition definition = MatchTestCards.Spell("FIREBALL", cost: 4);

            CardInstance card = CardInstance.FromDefinition(definition, instanceId: 1, ownerId: 0);

            Assert.That(card.CardKey, Is.EqualTo("FIREBALL"));
            Assert.That(card.Attack, Is.EqualTo(0));
            Assert.That(card.MaxHealth, Is.EqualTo(0));
            Assert.That(card.Health, Is.EqualTo(0));
        }

        [Test]
        public void FromDefinition_WhenDefinitionNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => CardInstance.FromDefinition(null!, instanceId: 1, ownerId: 0));
        }

        [Test]
        public void FromDefinition_WhenInstanceIdNegative_Throws()
        {
            CardDefinition definition = MatchTestCards.Minion();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => CardInstance.FromDefinition(definition, instanceId: -1, ownerId: 0));
        }

        [Test]
        public void FromDefinition_WhenOwnerIdNegative_Throws()
        {
            CardDefinition definition = MatchTestCards.Minion();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => CardInstance.FromDefinition(definition, instanceId: 1, ownerId: -1));
        }

        [Test]
        public void FromDefinition_CopiesKeywordsIntoKeywordSet()
        {
            CardDefinition definition = MatchTestCards.Minion(
                "TAUNT_CHARGE", keywords: Keyword.Taunt | Keyword.Charge);

            CardInstance card = CardInstance.FromDefinition(definition, 1, 0);

            Assert.That(card.Keywords.Has(Keyword.Taunt), Is.True);
            Assert.That(card.Keywords.Has(Keyword.Charge), Is.True);
            Assert.That(card.Keywords.Has(Keyword.Windfury), Is.False);
            Assert.That(card.Statuses.IsEmpty, Is.True);
        }

        [Test]
        public void FromDefinition_WhenDivineShield_PreloadsConsumableStatus()
        {
            CardDefinition definition = MatchTestCards.Minion(
                "SHIELDED", keywords: Keyword.DivineShield);

            CardInstance card = CardInstance.FromDefinition(definition, 1, 0);

            Assert.That(card.Statuses.Has(StatusFlags.DivineShield), Is.True);
            Assert.That(card.Statuses.ConsumeDivineShield(), Is.True);
            Assert.That(card.Statuses.Has(StatusFlags.DivineShield), Is.False);
        }

        [Test]
        public void FromDefinition_Spell_HasNoKeywordsOrStatuses()
        {
            CardDefinition definition = MatchTestCards.Spell();

            CardInstance card = CardInstance.FromDefinition(definition, 1, 0);

            Assert.That(card.Keywords.IsEmpty, Is.True);
            Assert.That(card.Statuses.IsEmpty, Is.True);
        }
    }
}
