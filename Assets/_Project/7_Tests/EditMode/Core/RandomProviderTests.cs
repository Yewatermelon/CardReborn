using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T4：`IRandomProvider` 的取值契约与"同种子可复现"。</summary>
    public sealed class RandomProviderTests
    {
        [Test]
        public void Seed_WhenCreated_ExposesConstructorSeed()
        {
            Assert.That(new SeededRandomProvider(2026).Seed, Is.EqualTo(2026));
            Assert.That(new SeededRandomProvider(-7).Seed, Is.EqualTo(-7));
            Assert.That(new SeededRandomProvider(0).Seed, Is.EqualTo(0));
        }

        [Test]
        public void NextInt_WhenSameSeed_ProducesSameSequence()
        {
            SeededRandomProvider first = new SeededRandomProvider(12345);
            SeededRandomProvider second = new SeededRandomProvider(12345);

            Assert.That(Draw(second, 50, 0, 1000), Is.EqualTo(Draw(first, 50, 0, 1000)));
        }

        [Test]
        public void NextInt_WhenDifferentSeeds_ProduceDifferentSequences()
        {
            List<int> first = Draw(new SeededRandomProvider(1), 20, 0, int.MaxValue);
            List<int> second = Draw(new SeededRandomProvider(2), 20, 0, int.MaxValue);

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void NextInt_WhenCalledManyTimes_StaysWithinRange()
        {
            SeededRandomProvider provider = new SeededRandomProvider(99);

            for (int i = 0; i < 1000; i++)
            {
                int value = provider.NextInt(-5, 10);
                Assert.That(value, Is.GreaterThanOrEqualTo(-5).And.LessThan(10));
            }
        }

        [Test]
        public void NextInt_WhenRangeHasSingleValue_AlwaysReturnsThatValue()
        {
            SeededRandomProvider provider = new SeededRandomProvider(5);

            for (int i = 0; i < 50; i++)
            {
                Assert.That(provider.NextInt(5, 6), Is.EqualTo(5));
            }
        }

        [TestCase(5, 5)]
        [TestCase(10, 5)]
        [TestCase(int.MaxValue, int.MinValue)]
        public void NextInt_WhenRangeIsEmptyOrReversed_ThrowsArgumentException(int min, int max)
        {
            SeededRandomProvider provider = new SeededRandomProvider(1);

            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => provider.NextInt(min, max))!;

            Assert.That(exception.ParamName, Is.EqualTo("minInclusive"));
        }

        [Test]
        public void NextInt_WhenSeedIsZero_ProducesVariedValues()
        {
            SeededRandomProvider provider = new SeededRandomProvider(0);

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                seen.Add(provider.NextInt(0, int.MaxValue));
            }

            Assert.That(seen.Count, Is.GreaterThan(5), "seed=0 若落入 xorshift 全零状态会永远输出 0");
        }

        [Test]
        public void NextInt_WhenSmallRange_CoversEveryValue()
        {
            SeededRandomProvider provider = new SeededRandomProvider(7);
            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < 400; i++)
            {
                seen.Add(provider.NextInt(0, 4));
            }

            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void NextInt_WhenRangeTouchesExtremes_StaysWithinBounds()
        {
            SeededRandomProvider provider = new SeededRandomProvider(31);

            for (int i = 0; i < 100; i++)
            {
                int nearMin = provider.NextInt(int.MinValue, int.MinValue + 2);
                Assert.That(nearMin, Is.EqualTo(int.MinValue).Or.EqualTo(int.MinValue + 1));

                int nearMax = provider.NextInt(int.MaxValue - 2, int.MaxValue);
                Assert.That(nearMax, Is.EqualTo(int.MaxValue - 2).Or.EqualTo(int.MaxValue - 1));

                int wide = provider.NextInt(int.MinValue, int.MaxValue);
                Assert.That(wide, Is.GreaterThanOrEqualTo(int.MinValue).And.LessThan(int.MaxValue));
            }
        }

        [Test]
        public void Providers_WhenSameSeedUsedInterleaved_DoNotAffectEachOther()
        {
            SeededRandomProvider first = new SeededRandomProvider(4242);
            SeededRandomProvider second = new SeededRandomProvider(4242);
            List<int> expected = Draw(new SeededRandomProvider(4242), 10, 0, 100);
            List<int> interleaved = new List<int>();

            for (int i = 0; i < 10; i++)
            {
                interleaved.Add(first.NextInt(0, 100));
                second.NextInt(0, 100);
            }

            Assert.That(interleaved, Is.EqualTo(expected));
        }

        private static List<int> Draw(IRandomProvider provider, int count, int min, int max)
        {
            List<int> values = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(provider.NextInt(min, max));
            }

            return values;
        }
    }
}
