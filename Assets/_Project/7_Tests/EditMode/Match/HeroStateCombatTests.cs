using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T4 AC-2/4：HeroState 伤害（护甲先抵）与治疗（不超上限）。</summary>
    [TestFixture]
    public sealed class HeroStateCombatTests
    {
        [Test]
        public void TakeDamage_NoArmor_ReducesHealth()
        {
            HeroState hero = new HeroState("H", "P", 30);
            int dealt = hero.TakeDamage(5);
            Assert.That(dealt, Is.EqualTo(5));
            Assert.That(hero.Health, Is.EqualTo(25));
            Assert.That(hero.Armor, Is.EqualTo(0));
        }

        [Test]
        public void TakeDamage_WithArmor_ConsumesArmorFirst()
        {
            HeroState hero = new HeroState("H", "P", 30) { Armor = 3 };
            int dealt = hero.TakeDamage(5);
            Assert.That(dealt, Is.EqualTo(2));
            Assert.That(hero.Health, Is.EqualTo(28));
            Assert.That(hero.Armor, Is.EqualTo(0));
        }

        [Test]
        public void TakeDamage_ArmorAbsorbsAll_HealthUnchanged()
        {
            HeroState hero = new HeroState("H", "P", 30) { Armor = 10 };
            int dealt = hero.TakeDamage(4);
            Assert.That(dealt, Is.EqualTo(0));
            Assert.That(hero.Health, Is.EqualTo(30));
            Assert.That(hero.Armor, Is.EqualTo(6));
        }

        [Test]
        public void TakeDamage_ExceedsHealth_HealthCanGoToZeroOrBelow()
        {
            HeroState hero = new HeroState("H", "P", 10);
            int dealt = hero.TakeDamage(15);
            Assert.That(dealt, Is.EqualTo(15));
            Assert.That(hero.Health, Is.EqualTo(-5));
        }

        [Test]
        public void Heal_DoesNotExceedMaxHealth()
        {
            HeroState hero = new HeroState("H", "P", 30, health: 20);
            int healed = hero.Heal(15);
            Assert.That(healed, Is.EqualTo(10));
            Assert.That(hero.Health, Is.EqualTo(30));
        }

        [Test]
        public void Heal_FullHealth_NoEffect()
        {
            HeroState hero = new HeroState("H", "P", 30);
            int healed = hero.Heal(5);
            Assert.That(healed, Is.EqualTo(0));
            Assert.That(hero.Health, Is.EqualTo(30));
        }
    }
}
