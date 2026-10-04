using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>
    /// M1-T4：Fisher–Yates 洗牌的语义与不变式。
    /// 含 [04 §12 案例 3](../Docs/04-代码复盘Review规范.md) 的回归：洗牌对象必须就是被索引的那个集合。
    /// </summary>
    public sealed class ShuffleTests
    {
        [Test]
        public void Shuffle_WhenListIsNull_ThrowsArgumentNullException()
        {
            SeededRandomProvider provider = new SeededRandomProvider(1);

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => provider.Shuffle<int>(null));

            Assert.That(exception.ParamName, Is.EqualTo("list"));
        }

        [Test]
        public void Shuffle_WhenListIsEmptyOrSingle_DoesNotConsumeRandomness()
        {
            SeededRandomProvider shuffled = new SeededRandomProvider(8);
            List<int> empty = new List<int>();
            shuffled.Shuffle(empty);
            List<int> afterEmpty = Draw(shuffled, 5);

            SeededRandomProvider untouched = new SeededRandomProvider(8);
            List<int> baseline = Draw(untouched, 5);

            Assert.That(empty, Is.Empty);
            Assert.That(afterEmpty, Is.EqualTo(baseline), "空列表洗牌不应消耗随机数");
        }

        [Test]
        public void Shuffle_WhenSingleElement_LeavesListUnchanged()
        {
            SeededRandomProvider provider = new SeededRandomProvider(3);
            List<string> deck = new List<string> { "only-card" };

            provider.Shuffle(deck);

            Assert.That(deck, Is.EqualTo(new[] { "only-card" }));
        }

        [Test]
        public void Shuffle_WhenCalled_KeepsEveryElementExactlyOnce()
        {
            SeededRandomProvider provider = new SeededRandomProvider(11);
            List<string> deck = new List<string> { "a", "b", "c", "a", "d", "b", "e", "a" };
            List<string> before = new List<string>(deck);

            provider.Shuffle(deck);

            deck.Sort();
            before.Sort();
            Assert.That(deck, Is.EqualTo(before), "洗牌只能重排，不能丢元素或复制元素");
        }

        [Test]
        public void Shuffle_WhenCalled_ReordersThePassedInstanceInPlace()
        {
            SeededRandomProvider provider = new SeededRandomProvider(2026);
            List<int> deck = CreateDeck(20);
            List<int> original = new List<int>(deck);

            provider.Shuffle(deck);

            Assert.That(deck, Is.Not.EqualTo(original), "传入的实例本身应被重排");
            Assert.That(deck.Count, Is.EqualTo(20));
        }

        [Test]
        public void Shuffle_WhenSameSeed_ProducesSameOrder()
        {
            List<int> first = CreateDeck(30);
            List<int> second = CreateDeck(30);

            new SeededRandomProvider(555).Shuffle(first);
            new SeededRandomProvider(555).Shuffle(second);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Shuffle_ThenReadInIndexOrder_IsReproducibleDrawOrder()
        {
            List<string> first = CreateNamedDeck(10);
            List<string> second = CreateNamedDeck(10);
            List<string> before = new List<string>(first);

            new SeededRandomProvider(20261003).Shuffle(first);
            new SeededRandomProvider(20261003).Shuffle(second);

            Assert.That(first, Is.EqualTo(second), "同种子必须得到同一抽取顺序");
            Assert.That(first, Is.Not.EqualTo(before));
            Assert.That(first, Is.EquivalentTo(before), "抽取顺序必须是原牌库的一个排列");
        }

        [Test]
        public void Shuffle_WhenCalled_ConsumesRandomnessFromProvider()
        {
            SeededRandomProvider shuffled = new SeededRandomProvider(77);
            shuffled.Shuffle(CreateDeck(6));
            List<int> afterShuffle = Draw(shuffled, 5);

            SeededRandomProvider untouched = new SeededRandomProvider(77);
            List<int> baseline = Draw(untouched, 5);

            Assert.That(afterShuffle, Is.Not.EqualTo(baseline), "洗牌应推进随机序列");
        }

        [Test]
        public void Shuffle_WhenTwoElements_ProducesBothOrdersAcrossSeeds()
        {
            HashSet<int> firstElements = new HashSet<int>();

            for (int seed = 1; seed <= 40; seed++)
            {
                List<int> deck = new List<int> { 0, 1 };
                new SeededRandomProvider(seed).Shuffle(deck);
                firstElements.Add(deck[0]);
            }

            Assert.That(firstElements, Is.EquivalentTo(new[] { 0, 1 }), "两种顺序都应出现");
        }

        [Test]
        public void Shuffle_WhenCalledTwiceWithSameProvider_ProducesDifferentValidPermutations()
        {
            SeededRandomProvider provider = new SeededRandomProvider(31337);
            List<int> deck = CreateDeck(20);
            List<int> expected = CreateDeck(20);

            provider.Shuffle(deck);
            List<int> firstOrder = new List<int>(deck);
            provider.Shuffle(deck);

            Assert.That(firstOrder, Is.Not.EqualTo(deck));
            Assert.That(deck, Is.EquivalentTo(expected));
            Assert.That(firstOrder, Is.EquivalentTo(expected));
        }

        [Test]
        public void Shuffle_WhenDeckHasRepeatedCards_KeepsEachCount()
        {
            SeededRandomProvider provider = new SeededRandomProvider(909);
            List<string> deck = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                deck.Add("x");
                deck.Add("y");
            }

            provider.Shuffle(deck);

            int x = 0;
            int y = 0;
            foreach (string card in deck)
            {
                if (card == "x")
                {
                    x++;
                }
                else
                {
                    y++;
                }
            }

            Assert.That(x, Is.EqualTo(5));
            Assert.That(y, Is.EqualTo(5));
        }

        private static List<int> CreateDeck(int count)
        {
            List<int> deck = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                deck.Add(i);
            }

            return deck;
        }

        private static List<string> CreateNamedDeck(int count)
        {
            List<string> deck = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                deck.Add("card-" + i);
            }

            return deck;
        }

        private static List<int> Draw(IRandomProvider provider, int count)
        {
            List<int> values = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(provider.NextInt(0, int.MaxValue));
            }

            return values;
        }
    }
}
