using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 可注入种子的随机源（Docs/03 第 5.9.2 节与第 11.3 节）。
    /// 规则层只依赖本接口：对局与抽卡因此可复现（FR-4.6），且永远不必直接调用
    /// Unity 引擎自带的随机 API；Unity 适配实现放 <c>Card.Infrastructure</c>，
    /// 服务端实现与测试实现共用本接口。
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>返回 <c>[minInclusive, maxExclusive)</c> 区间内的均匀随机整数。</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>原地洗牌（Fisher–Yates）；<paramref name="list"/> 为 null 时抛 <see cref="System.ArgumentNullException"/>。</summary>
        void Shuffle<T>(IList<T> list);
    }
}
