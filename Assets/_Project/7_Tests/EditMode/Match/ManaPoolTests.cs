using System;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T1：法力水晶资源行为（FR-5.2：每回合上限 +1、回合开始回满）。</summary>
    public sealed class ManaPoolTests
    {
        [Test]
        public void Ctor_Default_IsEmpty()
        {
            ManaPool pool = new ManaPool();

            Assert.That(pool.Max, Is.EqualTo(0));
            Assert.That(pool.Current, Is.EqualTo(0));
        }

        [Test]
        public void Ctor_WithValues_ExposesMaxAndCurrent()
        {
            ManaPool pool = new ManaPool(max: 3, current: 2);

            Assert.That(pool.Max, Is.EqualTo(3));
            Assert.That(pool.Current, Is.EqualTo(2));
        }

        [Test]
        public void Ctor_WhenNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ManaPool(max: -1, current: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ManaPool(max: 0, current: -1));
        }

        [Test]
        public void CanSpend_ReflectsCurrent()
        {
            ManaPool pool = new ManaPool(max: 2, current: 2);

            Assert.That(pool.CanSpend(2), Is.True);
            Assert.That(pool.CanSpend(3), Is.False);
        }

        [Test]
        public void Spend_WhenAffordable_DeductsCurrent()
        {
            ManaPool pool = new ManaPool(max: 3, current: 3);

            var result = pool.Spend(2);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(pool.Current, Is.EqualTo(1));
            Assert.That(pool.Max, Is.EqualTo(3));
        }

        [Test]
        public void Spend_WhenExactCost_DrainsToZero()
        {
            ManaPool pool = new ManaPool(max: 2, current: 2);

            var result = pool.Spend(2);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(pool.Current, Is.EqualTo(0));
        }

        [Test]
        public void Spend_WhenInsufficient_ReturnsFailureAndKeepsCurrent()
        {
            ManaPool pool = new ManaPool(max: 1, current: 1);

            var result = pool.Spend(3);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_MANA_INSUFFICIENT"));
            Assert.That(pool.Current, Is.EqualTo(1));
        }

        [Test]
        public void Spend_WhenAmountNegative_ReturnsFailure()
        {
            ManaPool pool = new ManaPool(max: 1, current: 1);

            var result = pool.Spend(-1);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_AMOUNT_INVALID"));
            Assert.That(pool.Current, Is.EqualTo(1));
        }

        [Test]
        public void BeginTurn_FromEmpty_GrowsMaxAndRefills()
        {
            ManaPool pool = new ManaPool();

            pool.BeginTurn(manaLimit: 10);

            Assert.That(pool.Max, Is.EqualTo(1));
            Assert.That(pool.Current, Is.EqualTo(1));
        }

        [Test]
        public void BeginTurn_DoesNotCarryLeftoverAndRefillsToNewMax()
        {
            ManaPool pool = new ManaPool(max: 3, current: 1);

            pool.BeginTurn(manaLimit: 10);

            Assert.That(pool.Max, Is.EqualTo(4));
            Assert.That(pool.Current, Is.EqualTo(4));
        }

        [Test]
        public void BeginTurn_Repeatedly_CapsAtManaLimit()
        {
            ManaPool pool = new ManaPool();

            for (int turn = 0; turn < 11; turn++)
            {
                pool.BeginTurn(manaLimit: 10);
            }

            Assert.That(pool.Max, Is.EqualTo(10));
            Assert.That(pool.Current, Is.EqualTo(10));
        }

        [Test]
        public void BeginTurn_WhenManaLimitNotPositive_Throws()
        {
            ManaPool pool = new ManaPool();

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.BeginTurn(manaLimit: 0));
        }
    }
}
