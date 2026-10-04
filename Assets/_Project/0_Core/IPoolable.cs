namespace Card.Core
{
    /// <summary>
    /// 池对象生命周期回调（Docs/03 第 5.8 节第 2 条）：借出后 OnSpawn，归还时 OnDespawn 清理状态。
    /// 池不强制实现本接口；未实现的对象只是没有回调。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>从池中借出后调用。</summary>
        void OnSpawn();

        /// <summary>归还到池时调用；必须清理临时状态。</summary>
        void OnDespawn();
    }
}
