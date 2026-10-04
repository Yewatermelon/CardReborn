namespace Card.Core
{
    /// <summary>
    /// 逻辑时间抽象（Docs/03 第 5.9.2 节）：与真实时间解耦，便于测试与服务端推进。
    /// 规则层取时间一律走本接口，不直接调用系统时间或 Unity 时间；
    /// Unity 版实现放 Card.Infrastructure，服务端版放 Card.Server（M11-T3 接线）。
    /// </summary>
    public interface IClock
    {
        /// <summary>当前逻辑时刻（tick）。权威宿主按固定 tick 推进（Docs/05 第 4.3 节）。</summary>
        int CurrentTick { get; }
    }
}
