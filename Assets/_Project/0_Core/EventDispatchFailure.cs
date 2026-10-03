using System;

namespace Card.Core
{
    /// <summary>
    /// 订阅者异常被隔离后的失败记录，供日志与诊断消费（见 Docs/03 第 5.4 节第 4 条）。
    /// </summary>
    public readonly struct EventDispatchFailure
    {
        public EventDispatchFailure(Type eventType, string handlerName, Exception exception)
        {
            Guard.NotNull(eventType, nameof(eventType));
            Guard.NotNull(handlerName, nameof(handlerName));
            Guard.NotNull(exception, nameof(exception));

            EventType = eventType;
            HandlerName = handlerName;
            Exception = exception;
        }

        /// <summary>正在派发的事件类型。</summary>
        public Type EventType { get; }

        /// <summary>抛出异常的订阅者方法名（匿名 lambda 会是编译器生成的名称）。</summary>
        public string HandlerName { get; }

        /// <summary>被隔离的异常实例。</summary>
        public Exception Exception { get; }

        public override string ToString()
        {
            return "Event " + EventType.Name + " handler '" + HandlerName + "' failed: " + Exception.Message;
        }
    }
}
