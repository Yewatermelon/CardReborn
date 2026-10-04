using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 纯 BCL 的确定性随机源：**xorshift32** 生成序列 + **Fisher–Yates** 洗牌。
    ///
    /// 为什么不用 <see cref="System.Random"/>：其内部算法不保证跨 .NET 版本 / 运行时一致，
    /// 而本项目要求"同一种子 ⇒ 同一结果"（FR-4.6）。本实现只使用整数位移与异或，
    /// 因此同一实现 + 同一种子在任意运行时下序列一致。
    ///
    /// 边界说明：
    /// 1. 非加密安全，也不能防作弊——防作弊不在本阶段范围（Docs/05 第 15 节）；
    /// 2. 非线程安全（单线程 tick / 主线程约定）；
    /// 3. 不承诺"不同实现（如 Unity 版适配）之间逐位一致"；05 第 14 节已撤销该约束。
    /// </summary>
    public sealed class SeededRandomProvider : IRandomProvider
    {
        /// <summary>xorshift 的状态为 0 时会永远输出 0，故用一个非零常量替换。</summary>
        private const uint ZeroStateReplacement = 0x9E3779B9u;

        private uint _state;

        /// <summary>用给定种子创建随机源；<c>seed = 0</c> 同样可用。</summary>
        public SeededRandomProvider(int seed)
        {
            Seed = seed;

            // 先做乘法散列再兜底：避免相邻种子产生高度相关的起始状态。
            uint mixed = ((uint)seed * 2654435761u) + ZeroStateReplacement;
            _state = mixed == 0u ? ZeroStateReplacement : mixed;
        }

        /// <summary>构造时注入的种子（便于写入复盘日志）。</summary>
        public int Seed { get; }

        /// <summary>
        /// 返回 <c>[minInclusive, maxExclusive)</c> 内的均匀随机整数。
        /// 使用拒绝采样而非直接取模，避免低位取模偏置；区间为空时抛 <see cref="ArgumentException"/>。
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentException(
                    "区间必须非空：minInclusive 必须小于 maxExclusive。",
                    nameof(minInclusive));
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            uint limit = uint.MaxValue - (uint.MaxValue % range);

            uint sample;
            do
            {
                sample = NextUInt();
            }
            while (sample >= limit);

            return (int)(minInclusive + (long)(sample % range));
        }

        /// <summary>
        /// 原地洗牌，算法固定为 **Fisher–Yates（由后向前交换）**（Docs/03 第 5.9.2 节要求写明）。
        /// 空列表与单元素列表不会消耗随机数。
        /// </summary>
        public void Shuffle<T>(IList<T> list)
        {
            Guard.NotNull(list, nameof(list));

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i + 1);
                T current = list[i];
                list[i] = list[j];
                list[j] = current;
            }
        }

        private uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }
}
