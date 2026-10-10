namespace Card.Application.Match
{
    /// <summary>
    /// 权威对局宿主抽象（M11-T2）：LocalHost（PVE）/ PeerHost（房主模式）/ DedicatedHost（专用服务器，留接口）共用。
    /// 宿主持有唯一的 MatchHost 权威运行时——不同宿主只是"跑在哪里"的区别。
    /// </summary>
    public interface IMatchHost
    {
        /// <summary>当前权威状态版本（每接受一条命令 +1，拒绝不变）。</summary>
        ulong StateVersion { get; }

        /// <summary>对局是否已结束。</summary>
        bool IsFinished { get; }

        /// <summary>挂载一个客户端端点（宿主侧）。</summary>
        void Attach(LoopbackEndpoint endpoint);

        /// <summary>处理全部端点的待收上行消息；返回处理条数。</summary>
        int Pump();
    }
}
