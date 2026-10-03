namespace Card.Core
{
    /// <summary>一次事件派发的统计结果。</summary>
    public readonly struct PublishReport
    {
        public PublishReport(int handlerCount, int failureCount)
        {
            HandlerCount = handlerCount;
            FailureCount = failureCount;
        }

        /// <summary>本次实际调用的订阅者数量（含抛异常者）。</summary>
        public int HandlerCount { get; }

        /// <summary>本次被隔离的异常数量。</summary>
        public int FailureCount { get; }

        /// <summary>是否存在被隔离的异常。</summary>
        public bool HasFailures
        {
            get { return FailureCount > 0; }
        }

        public override string ToString()
        {
            return "PublishReport(handlers=" + HandlerCount + ", failures=" + FailureCount + ")";
        }
    }
}
