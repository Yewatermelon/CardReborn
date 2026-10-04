using System;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 运行时的"当前配置"句柄（M2-T6 热加载）：
    /// 1. 构造时必须已有配置（<see cref="Current"/> 永不为 null），否则说明组合根启动就没配好；
    /// 2. <see cref="TryReload"/> 重新加载：**成功才换库**，失败保留旧库并记录报告——绝不让卡池因一次坏配置变空；
    /// 3. 每次成功重载 <see cref="Version"/> +1，供 UI/View 判断"配置变了，需要刷新"；
    /// 4. 只依赖一个重新加载的委托（I/O 由 Infrastructure 注入），所以本类留在 Domain，可被内核测试与服务端复用。
    /// </summary>
    public sealed class ConfigService
    {
        private readonly Func<ConfigLoadResult> _reloadSource;

        /// <summary>用"启动时已就绪的配置"和"如何重新加载"创建服务。</summary>
        public ConfigService(CardDatabase initial, Func<ConfigLoadResult> reloadSource)
        {
            Current = Guard.NotNull(initial, nameof(initial));
            _reloadSource = Guard.NotNull(reloadSource, nameof(reloadSource));
        }

        /// <summary>当前生效的卡池；永不为 null。</summary>
        public CardDatabase Current { get; private set; }

        /// <summary>成功重载的次数（初始配置算第 0 次）。</summary>
        public int Version { get; private set; }

        /// <summary>最近一次重载结果；从未重载时为 null。</summary>
        public ConfigLoadResult? LastReload { get; private set; }

        /// <summary>重新加载配置；成功返回 true 并替换卡池，失败返回 false 且保留旧卡池。</summary>
        public bool TryReload()
        {
            ConfigLoadResult result = _reloadSource();
            LastReload = Guard.NotNull(result, nameof(result));

            if (!result.Succeeded || result.Bundle == null)
            {
                return false;
            }

            Current = new CardDatabase(result.Bundle);
            Version++;
            return true;
        }
    }
}
