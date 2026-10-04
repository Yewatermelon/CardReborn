namespace Card.Core
{
    /// <summary>
    /// 日志后端（Docs/03 第 5.9.2 节）：由组合根注入。
    /// 客户端实现写 Editor 控制台或文件，服务端实现写标准输出或文件；
    /// 实现应自行保证健壮性——<see cref="GameLog"/> 会兜底计数，但那只是最后一道防线。
    /// </summary>
    public interface ILogSink
    {
        void Write(LogLevel level, LogChannel channel, string message);
    }
}
