using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T6：闲置上限、扩容上限、`Clear` 与超限策略留痕。</summary>
    public sealed class ObjectPoolCapacityTests
    {
        [SetUp]
        public void SetUp()
        {
            GameLogTestHelper.Reset();
        }

        [Test]
        public void Return_WhenIdlePoolIsFull_DiscardsItemAndCounts()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxSize: 2);
            PooledItem first = pool.Rent();
            PooledItem second = pool.Rent();
            PooledItem third = pool.Rent();

            pool.Return(first);
            pool.Return(second);
            pool.Return(third);

            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(pool.DiscardedCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Return_WhenIdlePoolOverflows_RentStillCreatesNewItems()
        {
            CountingFactory<PooledItem> factory = new CountingFactory<PooledItem>(() => new PooledItem());
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(factory.Create, maxSize: 1);
            PooledItem first = pool.Rent();
            PooledItem second = pool.Rent();
            pool.Return(first);
            pool.Return(second);

            PooledItem rented = pool.Rent();
            pool.Rent();

            Assert.That(pool.DiscardedCount, Is.EqualTo(1));
            Assert.That(rented, Is.SameAs(first), "保留的是最早归还的对象");
            Assert.That(factory.Calls, Is.EqualTo(3));
        }

        [Test]
        public void Rent_WhenAtMaxCapacity_ThrowsInvalidOperationException()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxCapacity: 2);
            pool.Rent();
            pool.Rent();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => pool.Rent())!;

            Assert.That(exception.Message, Does.Contain("上限"));
            Assert.That(pool.CreatedCount, Is.EqualTo(2));
        }

        [Test]
        public void TryRent_WhenAtMaxCapacity_ReturnsFalseWithoutCreating()
        {
            CountingFactory<PooledItem> factory = new CountingFactory<PooledItem>(() => new PooledItem());
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(factory.Create, maxCapacity: 1);
            pool.Rent();

            bool rented = pool.TryRent(out PooledItem? item);

            Assert.That(rented, Is.False);
            Assert.That(item, Is.Null);
            Assert.That(factory.Calls, Is.EqualTo(1));
            Assert.That(pool.RejectedCount, Is.EqualTo(1));
        }

        [Test]
        public void TryRent_WhenCapacityAvailable_ReturnsItem()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxCapacity: 1);

            bool rented = pool.TryRent(out PooledItem? item);

            Assert.That(rented, Is.True);
            Assert.That(item, Is.Not.Null);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void TryRent_WhenItemReturned_ReusesInsteadOfRejecting()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxCapacity: 1);
            PooledItem first = pool.Rent();
            pool.Return(first);

            bool rented = pool.TryRent(out PooledItem? item);

            Assert.That(rented, Is.True);
            Assert.That(item, Is.SameAs(first));
            Assert.That(pool.RejectedCount, Is.EqualTo(0));
        }

        [Test]
        public void MaxCapacity_WhenZero_IsUnlimited()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());

            for (int i = 0; i < 50; i++)
            {
                pool.Rent();
            }

            Assert.That(pool.ActiveCount, Is.EqualTo(50));
            Assert.That(pool.RejectedCount, Is.EqualTo(0));
        }

        [Test]
        public void MaxSize_WhenZero_KeepsAllReturnedItems()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem first = pool.Rent();
            PooledItem second = pool.Rent();

            pool.Return(first);
            pool.Return(second);

            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(pool.DiscardedCount, Is.EqualTo(0));
        }

        [Test]
        public void Clear_WhenCalled_DropsIdleItemsOnly()
        {
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), prewarmCount: 2);
            PooledItem active = pool.Rent();

            pool.Clear();

            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));

            pool.Return(active);
            Assert.That(pool.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public void Clear_ThenRent_CreatesNewInstance()
        {
            CountingFactory<PooledItem> factory = new CountingFactory<PooledItem>(() => new PooledItem());
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(factory.Create, prewarmCount: 1);
            pool.Clear();

            PooledItem item = pool.Rent();

            Assert.That(item, Is.Not.Null);
            Assert.That(factory.Calls, Is.EqualTo(2), "清空后需要新建对象");
        }

        [Test]
        public void Discard_WhenFirstOccurs_LogsWarningOnce()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxSize: 1);
            PooledItem first = pool.Rent();
            PooledItem second = pool.Rent();

            pool.Return(first);
            pool.Return(second);

            Assert.That(pool.DiscardedCount, Is.EqualTo(1), "第二件超出闲置上限被丢弃");
            Assert.That(sink.Entries.Count, Is.EqualTo(1), "首次告警后只计数，避免刷屏");
            Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warn));
            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Perf));
        }

        [Test]
        public void Rent_WhenFirstRejected_LogsWarningOnce()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxCapacity: 1);
            pool.Rent();

            pool.TryRent(out PooledItem? _);
            pool.TryRent(out PooledItem? _);

            Assert.That(pool.RejectedCount, Is.EqualTo(2));
            Assert.That(sink.Entries.Count, Is.EqualTo(1));
            Assert.That(sink.Entries[0].Channel, Is.EqualTo(LogChannel.Perf));
        }

        [Test]
        public void Discard_WhenGameLogDisabled_CountsWithoutLogging()
        {
            RecordingLogSink sink = new RecordingLogSink();
            GameLog.Configure(sink, LogLevel.Info);
            GameLog.Disable();
            ObjectPool<PooledItem> pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxSize: 1);
            PooledItem first = pool.Rent();
            PooledItem second = pool.Rent();

            pool.Return(first);
            pool.Return(second);

            Assert.That(pool.DiscardedCount, Is.EqualTo(1));
            Assert.That(sink.Entries, Is.Empty);
        }
    }
}
