using System;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T6 AC-3/AC-4/AC-5：CardInstance.TakeDamage 圣盾/剧毒/0伤害。</summary>
    [TestFixture]
    public sealed class CardInstanceCombatTests
    {
        private static CardDefinition Minion(string key, int atk, int hp, Keyword keywords = Keyword.None)
        {
            return new CardDefinition
            {
                Id = Math.Abs(key.GetHashCode()),
                Key = key,
                Cost = 1,
                Type = CardType.Minion,
                Attack = atk,
                Health = hp,
                Keywords = keywords
            };
        }

        [Test]
        public void TakeDamage_Normal_ReducesHealth()
        {
            CardInstance c = CardInstance.FromDefinition(Minion("M", 2, 5), 1, 0);
            int lost = c.TakeDamage(3, poisonous: false);
            Assert.That(lost, Is.EqualTo(3));
            Assert.That(c.Health, Is.EqualTo(2));
        }

        [Test]
        public void TakeDamage_DivineShield_ConsumesAndBlocks()
        {
            CardInstance c = CardInstance.FromDefinition(
                Minion("DS", 2, 5, Keyword.DivineShield), 1, 0);
            Assert.That(c.Statuses.Has(StatusFlags.DivineShield), Is.True);

            int lost = c.TakeDamage(4, poisonous: false);

            Assert.That(lost, Is.EqualTo(0));
            Assert.That(c.Health, Is.EqualTo(5));
            Assert.That(c.Statuses.Has(StatusFlags.DivineShield), Is.False);
        }

        [Test]
        public void TakeDamage_Poisonous_SetsHealthToZero()
        {
            CardInstance c = CardInstance.FromDefinition(Minion("P", 2, 10), 1, 0);
            // 攻击力 1 的剧毒攻击者对其造成 1 伤害 → 必杀
            int lost = c.TakeDamage(1, poisonous: true);
            Assert.That(lost, Is.EqualTo(1));
            Assert.That(c.Health, Is.EqualTo(0));
        }

        [Test]
        public void TakeDamage_ZeroAmount_NoEffect_NoShieldConsume()
        {
            CardInstance c = CardInstance.FromDefinition(
                Minion("Z", 2, 5, Keyword.DivineShield), 1, 0);
            int lost = c.TakeDamage(0, poisonous: true);
            Assert.That(lost, Is.EqualTo(0));
            Assert.That(c.Health, Is.EqualTo(5));
            Assert.That(c.Statuses.Has(StatusFlags.DivineShield), Is.True);
        }

        [Test]
        public void TakeDamage_DivineShieldBlocksPoison()
        {
            CardInstance c = CardInstance.FromDefinition(
                Minion("DSP", 2, 5, Keyword.DivineShield), 1, 0);
            int lost = c.TakeDamage(3, poisonous: true);
            Assert.That(lost, Is.EqualTo(0));
            Assert.That(c.Health, Is.EqualTo(5));
            Assert.That(c.Statuses.Has(StatusFlags.DivineShield), Is.False);
        }
    }
}
