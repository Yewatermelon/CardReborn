using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T6：`ObjectPool<T>` 的借出、归还、生命周期回调与契约校验。</summary>
    public sealed class ObjectPoolTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLogTestHelper.Reset();
        }

        [Test]
        public void Rent_WhenPoolIsEmpty_CreatesItemThroughFactory()
        {
            CountingFactory<PooledItem> factory = new CountingFactory<PooledItem>(() => new PooledItem());
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(factory.Create);

            PooledItem item = pool.Rent();

            Assert.That(item, Is.Not.Null);
            Assert.That(factory.Calls, Is.EqualTo(1));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.EqualTo(0));
        }

        [Test]
        public void Rent_WhenIdleItemAvailable_ReusesSameInstance()
        {
            CountingFactory<PooledItem> factory = new CountingFactory<PooledItem>(() => new PooledItem());
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(factory.Create);
            PooledItem first = pool.Rent();
            pool.Return(first);

            PooledItem second = pool.Rent();

            Assert.That(second, Is.SameAs(first));
            Assert.That(factory.Calls, Is.EqualTo(1));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
        }

        [Test]
        public void Rent_WhenItemIsPoolable_CallsOnSpawnOnce()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());

            PooledItem item = pool.Rent();

            Assert.That(item.SpawnCount, Is.EqualTo(1));
            Assert.That(item.DespawnCount, Is.EqualTo(0));
        }

        [Test]
        public void Return_WhenItemIsPoolable_CallsOnDespawnAndClearsState()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem item = pool.Rent();
            item.Payload = "脏数据";

            pool.Return(item);

            Assert.That(item.DespawnCount, Is.EqualTo(1));
            Assert.That(item.Payload, Is.Null, "归还时必须清理状态（03 第 5.8 节第 2 条）");
            Assert.That(pool.IdleCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Return_ThenRent_ReturnsSameInstanceWithCleanState()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem first = pool.Rent();
            first.Payload = "脏数据";
            pool.Return(first);

            PooledItem second = pool.Rent();

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Payload, Is.Null);
            Assert.That(second.SpawnCount, Is.EqualTo(2));
            Assert.That(second.DespawnCount, Is.EqualTo(1));
        }

        [Test]
        public void Rent_WhenItemIsNotPoolable_WorksWithoutCallbacks()
        {
            ObjectPool<PlainItem> pool = new ObjectPool<PlainItem>(() => new PlainItem());

            PlainItem item = pool.Rent();
            pool.Return(item);

            Assert.That(pool.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public void Ctor_WhenPrewarmCountProvided_CreatesIdleItemsUpFront()
        {
            CountingFactory<PooledItem> factory = new CountingFactory<PooledItem>(() => new PooledItem());

            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(factory.Create, prewarmCount: 3);

            Assert.That(factory.Calls, Is.EqualTo(3));
            Assert.That(pool.IdleCount, Is.EqualTo(3));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.CreatedCount, Is.EqualTo(3));
        }

        [Test]
        public void Ctor_WhenFactoryIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => new ObjectPool<PooledItem>(null!));

            Assert.That(exception.ParamName, Is.EqualTo("factory"));
        }

        [Test]
        public void Ctor_WhenPrewarmCountIsNegative_ThrowsArgumentOutOfRangeException()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ObjectPool<PooledItem>(() => new PooledItem(), prewarmCount: -1));

            Assert.That(exception.ParamName, Is.EqualTo("prewarmCount"));
        }

        [Test]
        public void Ctor_WhenPrewarmExceedsCapacity_ThrowsArgumentException()
        {
            ObjectPool<PooledItem>? created = null;

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => created = new ObjectPool<PooledItem>(() => new PooledItem(), prewarmCount: 5, maxCapacity: 2));

            Assert.That(exception.ParamName, Is.EqualTo("prewarmCount"));
            Assert.That(created, Is.Null, "配置矛盾时不应产生半成品池");
        }

        [Test]
        public void Factory_WhenReturnsNull_ThrowsInvalidOperationException()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => null!);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => pool.Rent());

            Assert.That(exception.Message, Does.Contain("null"));
        }

        [Test]
        public void Counts_WhenRentingAndReturning_StayConsistent()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), prewarmCount: 2);

            PooledItem first = pool.Rent();
            PooledItem second = pool.Rent();

            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(2));

            pool.Return(first);
            pool.Return(second);

            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.CreatedCount, Is.EqualTo(2));
        }

        [Test]
        public void Return_WhenItemIsNull_ThrowsArgumentNullException()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => pool.Return(null!));

            Assert.That(exception.ParamName, Is.EqualTo("item"));
        }

        [Test]
        public void Return_WhenItemWasNotRentedFromThisPool_ThrowsInvalidOperationException()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem foreign = new PooledItem();

            Assert.Throws<InvalidOperationException>(() => pool.Return(foreign));
            Assert.That(pool.IdleCount, Is.EqualTo(0), "外来对象不得进入闲置列表");
        }

        [Test]
        public void Return_WhenSameItemReturnedTwice_ThrowsInvalidOperationException()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem item = pool.Rent();
            pool.Return(item);

            Assert.Throws<InvalidOperationException>(() => pool.Return(item));
            Assert.That(pool.IdleCount, Is.EqualTo(1), "重复归还不应产生副本");
        }

        [Test]
        public void Return_WhenDoubleReturnAttempted_PoolStaysUsable()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem item = pool.Rent();
            pool.Return(item);
            Assert.Throws<InvalidOperationException>(() => pool.Return(item));

            PooledItem reused = pool.Rent();
            pool.Return(reused);

            Assert.That(reused, Is.SameAs(item));
            Assert.That(pool.IdleCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }
    }
}
