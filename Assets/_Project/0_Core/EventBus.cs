using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 类型安全的事件总线：规则层产出"过去式事实"，表现层只读消费（Docs/03 第 5.4 节）。
    ///
    /// 语义约定：
    /// 1. 事件按类型路由，只有同类型订阅者会被调用；
    /// 2. 订阅返回 <see cref="IDisposable"/>，解绑幂等；View 在 OnEnable 订阅、OnDisable 解绑；
    /// 3. 派发期间新增的订阅不参与本次派发；派发期间解绑且尚未调用的订阅会被跳过；
    /// 4. 单个订阅者抛异常被隔离（记录后继续派发其余订阅者），并计入 <see cref="PublishReport"/>；
    ///    隔离不等于解绑，出错的订阅者仍会参与后续派发；
    /// 5. 事件顺序 = 派发顺序，调用方（Application 层）必须在状态结算完成后一次性派发；
    /// 6. <b>非线程安全</b>：约定单线程使用（服务器 tick / 主线程）。
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>();
        private readonly IEventDispatchFailureSink _failureSink;
        private int _dispatchDepth;

        /// <summary>创建不带失败出口的总线：失败只体现在 <see cref="PublishReport"/> 中。</summary>
        public EventBus()
        {
        }

        /// <summary>创建带失败出口的总线：建议由外层接到 <c>GameLog</c>。</summary>
        public EventBus(IEventDispatchFailureSink failureSink)
        {
            _failureSink = failureSink;
        }

        /// <summary>订阅指定类型的事件；返回的 <see cref="IDisposable"/> 用于解绑（可重复调用）。</summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            Guard.NotNull(handler, nameof(handler));

            List<Subscription<TEvent>> handlers = GetOrCreateChannel<TEvent>();
            Subscription<TEvent> subscription = new Subscription<TEvent>(this, handlers, handler);
            handlers.Add(subscription);
            return subscription;
        }

        /// <summary>向所有订阅者派发事件（引用类型事件不可为 null）。</summary>
        public PublishReport Publish<TEvent>(TEvent evt)
        {
            ThrowIfReferenceTypeEventIsNull(evt);

            if (!_channels.TryGetValue(typeof(TEvent), out object channelObject))
            {
                return default;
            }

            List<Subscription<TEvent>> handlers = (List<Subscription<TEvent>>)channelObject;
            int plannedCount = handlers.Count;
            int invokedCount = 0;
            int failureCount = 0;

            _dispatchDepth++;
            try
            {
                for (int i = 0; i < plannedCount; i++)
                {
                    Subscription<TEvent> subscription = handlers[i];
                    if (!subscription.IsActive)
                    {
                        continue;
                    }

                    invokedCount++;
                    try
                    {
                        subscription.Handler(evt);
                    }
                    catch (Exception exception)
                    {
                        failureCount++;
                        ReportFailure(typeof(TEvent), subscription.Handler, exception);
                    }
                }
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0)
                {
                    Compact(handlers);
                }
            }

            return new PublishReport(invokedCount, failureCount);
        }

        /// <summary>当前活跃订阅者数量（诊断与测试用）。</summary>
        public int SubscriberCount<TEvent>()
        {
            if (!_channels.TryGetValue(typeof(TEvent), out object channelObject))
            {
                return 0;
            }

            List<Subscription<TEvent>> handlers = (List<Subscription<TEvent>>)channelObject;
            int count = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                if (handlers[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private List<Subscription<TEvent>> GetOrCreateChannel<TEvent>()
        {
            if (_channels.TryGetValue(typeof(TEvent), out object existing))
            {
                return (List<Subscription<TEvent>>)existing;
            }

            List<Subscription<TEvent>> created = new List<Subscription<TEvent>>();
            _channels.Add(typeof(TEvent), created);
            return created;
        }

        private void ReportFailure<TEvent>(Type eventType, Action<TEvent> handler, Exception exception)
        {
            if (_failureSink == null)
            {
                return;
            }

            string handlerName = handler.Method != null ? handler.Method.Name : handler.ToString();
            _failureSink.OnHandlerFailed(new EventDispatchFailure(eventType, handlerName, exception));
        }

        private static void ThrowIfReferenceTypeEventIsNull<TEvent>(TEvent evt)
        {
            // 值类型事件不可能为 null，且此分支不会被取到，因此不会产生装箱。
            if (typeof(TEvent).IsValueType)
            {
                return;
            }

            object boxed = evt;
            if (boxed == null)
            {
                throw new ArgumentNullException(nameof(evt), "引用类型事件不能为 null。");
            }
        }

        private static void Compact<TEvent>(List<Subscription<TEvent>> handlers)
        {
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                if (!handlers[i].IsActive)
                {
                    handlers.RemoveAt(i);
                }
            }
        }

        private sealed class Subscription<TEvent> : IDisposable
        {
            private readonly EventBus _owner;
            private readonly List<Subscription<TEvent>> _handlers;
            private readonly Action<TEvent> _handler;

            public Subscription(EventBus owner, List<Subscription<TEvent>> handlers, Action<TEvent> handler)
            {
                _owner = owner;
                _handlers = handlers;
                _handler = handler;
            }

            public Action<TEvent> Handler
            {
                get { return _handler; }
            }

            public bool IsActive { get; private set; } = true;

            public void Dispose()
            {
                if (!IsActive)
                {
                    return;
                }

                IsActive = false;

                // 派发进行中只标记失效，等最外层派发结束后统一压缩，避免迭代期间改动集合。
                if (_owner._dispatchDepth == 0)
                {
                    _handlers.Remove(this);
                }
            }
        }
    }
}
