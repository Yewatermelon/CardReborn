using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T3：StatusSet 位标记（召唤失调/冻结/圣盾）与圣盾消耗一次。</summary>
    public sealed class StatusSetTests
    {
        [Test]
        public void Ctor_Default_IsEmpty()
        {
            StatusSet set = new StatusSet();

            Assert.That(set.Flags, Is.EqualTo(StatusFlags.None));
            Assert.That(set.IsEmpty, Is.True);
        }

        [Test]
        public void Add_SetsBitsAndHasIsTrue()
        {
            StatusSet set = new StatusSet();

            set.Add(StatusFlags.SummoningSickness);

            Assert.That(set.Has(StatusFlags.SummoningSickness), Is.True);
            Assert.That(set.Has(StatusFlags.Frozen), Is.False);
        }

        [Test]
        public void Add_CombinedFlags_StoresAllBits()
        {
            StatusSet set = new StatusSet();

            set.Add(StatusFlags.Frozen | StatusFlags.DivineShield);

            Assert.That(set.Has(StatusFlags.Frozen), Is.True);
            Assert.That(set.Has(StatusFlags.DivineShield), Is.True);
            Assert.That(set.Has(StatusFlags.SummoningSickness), Is.False);
        }

        [Test]
        public void Add_IsIdempotent()
        {
            StatusSet set = new StatusSet();

            set.Add(StatusFlags.Frozen);
            set.Add(StatusFlags.Frozen);

            Assert.That(set.Flags, Is.EqualTo(StatusFlags.Frozen));
        }

        [Test]
        public void Remove_ClearsGivenBits()
        {
            StatusSet set = new StatusSet(StatusFlags.Frozen | StatusFlags.DivineShield);

            set.Remove(StatusFlags.Frozen);

            Assert.That(set.Has(StatusFlags.Frozen), Is.False);
            Assert.That(set.Has(StatusFlags.DivineShield), Is.True);
        }

        [Test]
        public void Remove_WhenBitAbsent_StaysUnchanged()
        {
            StatusSet set = new StatusSet(StatusFlags.DivineShield);

            set.Remove(StatusFlags.Frozen);

            Assert.That(set.Flags, Is.EqualTo(StatusFlags.DivineShield));
        }

        [Test]
        public void Toggle_SetsOrClearsBits()
        {
            StatusSet set = new StatusSet();

            set.Toggle(StatusFlags.SummoningSickness, enabled: true);
            Assert.That(set.Has(StatusFlags.SummoningSickness), Is.True);

            set.Toggle(StatusFlags.SummoningSickness, enabled: false);
            Assert.That(set.Has(StatusFlags.SummoningSickness), Is.False);
        }

        [Test]
        public void Clear_RemovesEveryStatus()
        {
            StatusSet set = new StatusSet(
                StatusFlags.SummoningSickness | StatusFlags.Frozen | StatusFlags.DivineShield);

            set.Clear();

            Assert.That(set.IsEmpty, Is.True);
        }

        [Test]
        public void ConsumeDivineShield_WhenPresent_RemovesOnceAndReturnsTrue()
        {
            StatusSet set = new StatusSet(StatusFlags.DivineShield | StatusFlags.Frozen);

            bool first = set.ConsumeDivineShield();
            bool second = set.ConsumeDivineShield();

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(set.Has(StatusFlags.DivineShield), Is.False);
            Assert.That(set.Has(StatusFlags.Frozen), Is.True);
        }

        [Test]
        public void ConsumeDivineShield_WhenAbsent_ReturnsFalse()
        {
            StatusSet set = new StatusSet(StatusFlags.Frozen);

            bool consumed = set.ConsumeDivineShield();

            Assert.That(consumed, Is.False);
            Assert.That(set.Flags, Is.EqualTo(StatusFlags.Frozen));
        }
    }
}
