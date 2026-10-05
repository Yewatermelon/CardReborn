using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T3：运行时 KeywordSet 的位标记增删查。</summary>
    public sealed class KeywordSetTests
    {
        [Test]
        public void Ctor_Default_IsEmpty()
        {
            KeywordSet set = new KeywordSet();

            Assert.That(set.Flags, Is.EqualTo(Keyword.None));
            Assert.That(set.IsEmpty, Is.True);
        }

        [Test]
        public void Ctor_WithFlags_ExposesThem()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt | Keyword.Charge);

            Assert.That(set.IsEmpty, Is.False);
            Assert.That(set.Flags, Is.EqualTo(Keyword.Taunt | Keyword.Charge));
        }

        [Test]
        public void Has_ReflectsContainedBits()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt | Keyword.Charge);

            Assert.That(set.Has(Keyword.Taunt), Is.True);
            Assert.That(set.Has(Keyword.Windfury), Is.False);
        }

        [Test]
        public void HasAll_RequiresEveryBit()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt | Keyword.Charge);

            Assert.That(set.HasAll(Keyword.Taunt | Keyword.Charge), Is.True);
            Assert.That(set.HasAll(Keyword.Taunt | Keyword.Windfury), Is.False);
        }

        [Test]
        public void HasAny_MatchesOneBitOrMore()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt);

            Assert.That(set.HasAny(Keyword.Taunt | Keyword.Windfury), Is.True);
            Assert.That(set.HasAny(Keyword.Charge | Keyword.Windfury), Is.False);
        }

        [Test]
        public void Add_SetsBitsAndIsIdempotent()
        {
            KeywordSet set = new KeywordSet();

            set.Add(Keyword.Taunt);
            set.Add(Keyword.Taunt);

            Assert.That(set.Flags, Is.EqualTo(Keyword.Taunt));

            set.Add(Keyword.Charge | Keyword.Windfury);

            Assert.That(set.HasAll(Keyword.Taunt | Keyword.Charge | Keyword.Windfury), Is.True);
        }

        [Test]
        public void Remove_ClearsOnlyGivenBits()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt | Keyword.Charge);

            set.Remove(Keyword.Taunt);

            Assert.That(set.Has(Keyword.Taunt), Is.False);
            Assert.That(set.Has(Keyword.Charge), Is.True);
        }

        [Test]
        public void Remove_WhenBitAbsent_StaysUnchanged()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt);

            set.Remove(Keyword.Windfury);

            Assert.That(set.Flags, Is.EqualTo(Keyword.Taunt));
        }

        [Test]
        public void Toggle_SetsOrClearsBits()
        {
            KeywordSet set = new KeywordSet();

            set.Toggle(Keyword.Taunt, enabled: true);
            Assert.That(set.Has(Keyword.Taunt), Is.True);

            set.Toggle(Keyword.Taunt, enabled: false);
            Assert.That(set.Has(Keyword.Taunt), Is.False);
        }

        [Test]
        public void Clear_RemovesAllKeywords()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt | Keyword.Charge | Keyword.Windfury);

            set.Clear();

            Assert.That(set.IsEmpty, Is.True);
            Assert.That(set.Flags, Is.EqualTo(Keyword.None));
        }

        [Test]
        public void CombinedFlags_CanBeRemovedOneByOne()
        {
            KeywordSet set = new KeywordSet(Keyword.Taunt | Keyword.Charge | Keyword.Windfury);

            set.Remove(Keyword.Taunt);
            set.Remove(Keyword.Charge);

            Assert.That(set.Flags, Is.EqualTo(Keyword.Windfury));
            Assert.That(set.HasAny(Keyword.Taunt | Keyword.Charge), Is.False);
        }
    }
}
