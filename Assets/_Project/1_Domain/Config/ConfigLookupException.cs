using System;

namespace Card.Domain.Config
{
    /// <summary>
    /// 运行时查配置查不到（如卡牌 id/key 不存在）。FR-1.4 要求"查不到返回明确错误而非 null 崩溃"。
    /// </summary>
    public sealed class ConfigLookupException : Exception
    {
        public ConfigLookupException(string message)
            : base(message)
        {
        }
    }
}
