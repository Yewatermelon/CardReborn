using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Card.Core
{
    /// <summary>
    /// 通用对象池（Docs/03 第 5.8 节）：预热、复用、归还清理、闲置上限与扩容上限。
    ///
    /// 约定：
    /// 1. maxSize 限制闲置对象数量，超出即丢弃并计入 DiscardedCount；
    /// 2. maxCapacity 限制对象总数（活跃 + 闲置），达到后不再创建：Rent 抛异常、TryRent 返回 false 并计入 RejectedCount；
    /// 3. 两个上限传 0 都表示不限制；超限首次发生会通过 GameLog 打一条 Warn，之后只计数；
    /// 4. 重复归还、归还非本池对象会立即抛异常——这类误用会悄悄污染池，必须早暴露；
    /// 5. 非线程安全（单线程 tick / 主线程约定）。
    ///
    /// 只用于池化表现对象（03 第 5.8 节第 4 条）；领域对象请用结构或小对象。
    /// </summary>
    /// <typeparam name="T">池化对象类型（引用类型）。</typeparam>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly int _maxSize;
        private readonly int _maxCapacity;
        private readonly List<T> _idle = new List<T>();
        private readonly HashSet<T> _active = new HashSet<T>(ReferenceComparer.Instance);

        private int _createdCount;
        private int _discardedCount;
        private int _rejectedCount;
        private bool _warnedOnDiscard;
        private bool _warnedOnReject;

        /// <summary>创建对象池；两个上限传 0 表示不限制。</summary>
        public ObjectPool(Func<T> factory, int prewarmCount = 0, int maxSize = 0, int maxCapacity = 0)
        {
            _factory = Guard.NotNull(factory, nameof(factory));

            if (prewarmCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prewarmCount), prewarmCount, "预热数量不能为负数。");
            }

            if (maxSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "闲置上限不能为负数。");
            }

            if (maxCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), maxCapacity, "扩容上限不能为负数。");
            }

            if (maxCapacity > 0 && prewarmCount > maxCapacity)
            {
                throw new ArgumentException("预热数量不能超过扩容上限。", nameof(prewarmCount));
            }

            _maxSize = maxSize;
            _maxCapacity = maxCapacity;

            for (int i = 0; i < prewarmCount; i++)
            {
                _idle.Add(CreateItem());
            }
        }

        /// <summary>当前闲置对象数量。</summary>
        public int IdleCount
        {
            get { return _idle.Count; }
        }

        /// <summary>当前借出（在借）对象数量。</summary>
        public int ActiveCount
        {
            get { return _active.Count; }
        }

        /// <summary>工厂累计创建的对象数量。</summary>
        public int CreatedCount
        {
            get { return _createdCount; }
        }

        /// <summary>因闲置上限被丢弃的对象数量。</summary>
        public int DiscardedCount
        {
            get { return _discardedCount; }
        }

        /// <summary>因扩容上限被拒绝的借出次数。</summary>
        public int RejectedCount
        {
            get { return _rejectedCount; }
        }

        /// <summary>借出对象；已达扩容上限且无闲置对象时抛 InvalidOperationException。</summary>
        public T Rent()
        {
            if (TryRent(out T? item))
            {
                return item!;
            }

            throw new InvalidOperationException("对象池已达扩容上限，无法借出：" + typeof(T).Name);
        }

        /// <summary>尝试借出对象；已达扩容上限且无闲置对象时返回 false。</summary>
        public bool TryRent(out T? item)
        {
            if (_idle.Count > 0)
            {
                int index = _idle.Count - 1;
                item = _idle[index];
                _idle.RemoveAt(index);
                _active.Add(item);
                (item as IPoolable)?.OnSpawn();
                return true;
            }

            if (_maxCapacity > 0 && _createdCount >= _maxCapacity)
            {
                item = null;
                _rejectedCount++;
                WarnOnce(ref _warnedOnReject, "对象池已达扩容上限，借出被拒绝（后续只计数）：");
                return false;
            }

            item = CreateItem();
            _active.Add(item);
            (item as IPoolable)?.OnSpawn();
            return true;
        }

        /// <summary>归还对象：先调用 IPoolable.OnDespawn 清理，再纳入闲置列表。</summary>
        public void Return(T item)
        {
            Guard.NotNull(item, nameof(item));

            if (!_active.Remove(item))
            {
                throw new InvalidOperationException("归还了非本池借出或已归还的对象：" + typeof(T).Name);
            }

            (item as IPoolable)?.OnDespawn();

            if (_maxSize > 0 && _idle.Count >= _maxSize)
            {
                _discardedCount++;
                WarnOnce(ref _warnedOnDiscard, "对象池闲置上限已满，归还对象被丢弃（后续只计数）：");
                return;
            }

            _idle.Add(item);
        }

        /// <summary>丢弃全部闲置对象；在借对象不受影响。</summary>
        public void Clear()
        {
            _idle.Clear();
        }

        private T CreateItem()
        {
            T item = _factory();
            if (item == null)
            {
                throw new InvalidOperationException("对象池工厂返回了 null：" + typeof(T).Name);
            }

            _createdCount++;
            return item;
        }

        private static void WarnOnce(ref bool warned, string message)
        {
            if (warned)
            {
                return;
            }

            warned = true;

            if (GameLog.IsEnabled(LogChannel.Perf, LogLevel.Warn))
            {
                GameLog.Warn(LogChannel.Perf, message + typeof(T).Name);
            }
        }

        /// <summary>引用相等比较器：池必须按"同一个对象"判断，不受 T 重写 Equals 的影响。</summary>
        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(T? left, T? right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
