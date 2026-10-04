namespace Card.Core
{
    /// <summary>
    /// 日志等级（Docs/03 第 8 节）：数值越大越重要，低于最小等级的日志会被过滤。
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试细节，默认关闭。</summary>
        Trace = 0,

        /// <summary>常规信息。</summary>
        Info = 1,

        /// <summary>需要关注但不影响继续运行的情况。</summary>
        Warn = 2,

        /// <summary>错误。</summary>
        Error = 3
    }
}
