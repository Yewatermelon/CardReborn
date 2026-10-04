using System;

namespace Card.Core
{
    /// <summary>
    /// 把事件派发失败（M1-T2 的 <see cref="IEventDispatchFailureSink"/>）接到 <see cref="GameLog"/>：
    /// 订阅者抛异常既能被隔离，又能留下可定位的日志（Docs/03 第 5.4 节第 4 条）。
    /// </summary>
    public sealed class EventDispatchLogSink : IEventDispatchFailureSink
    {
        private readonly LogChannel _channel;

        /// <summary>创建桥接；默认写入 <see cref="LogChannel.Match"/> 通道。</summary>
        public EventDispatchLogSink(LogChannel channel = LogChannel.Match)
        {
            if (!GameLog.IsDefinedChannel(channel))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channel),
                    channel,
                    "未定义的日志通道。");
            }

            _channel = channel;
        }

        /// <inheritdoc />
        public void OnHandlerFailed(EventDispatchFailure failure)
        {
            GameLog.Error(_channel, "事件订阅者抛异常：" + failure, failure.Exception);
        }
    }
}
