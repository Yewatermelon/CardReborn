using System;
using Card.Core;

namespace Card.Tests.EditMode.Core
{
    /// <summary>实现 IPoolable 的池对象：记录生命周期回调次数并在归还时清理状态。</summary>
    internal sealed class PooledItem : IPoolable
    {
        public int SpawnCount { get; private set; }

        public int DespawnCount { get; private set; }

        public string? Payload { get; set; }

        public void OnSpawn()
        {
            SpawnCount++;
        }

        public void OnDespawn()
        {
            DespawnCount++;
            Payload = null;
        }
    }

    /// <summary>不实现 IPoolable 的普通类型：验证池不强制接口。</summary>
    internal sealed class PlainItem
    {
        public string? Payload { get; set; }
    }

    /// <summary>可计数的工厂（借出次数断言用）。</summary>
    internal sealed class CountingFactory<T> where T : class
    {
        private readonly Func<T> _factory;

        public CountingFactory(Func<T> factory)
        {
            _factory = factory;
        }

        public int Calls { get; private set; }

        public T Create()
        {
            Calls++;
            return _factory();
        }
    }
}
