using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 可观察的标量值（Docs/00 第 4.1 节 Core 内容清单）：值变化时通知订阅者，供 View 只读订阅。
    ///
    /// 约定：
    /// 1. 只有值真正变化才通知（用 EqualityComparer&lt;T&gt;.Default 判定），相同值不打扰订阅者；
    /// 2. 订阅返回 IDisposable，解绑幂等；订阅者必须显式 Dispose（不做弱引用订阅）；
    /// 3. 派发期间新增的订阅不参与本次通知；派发期间解绑且尚未通知的订阅者会被跳过；
    /// 4. 通知过程中值再次变化时，<b>旧派发会中止剩余订阅者</b>，由新派发接管——避免 View 收到过期值；
    /// 5. 单个订阅者抛异常被隔离（记录后继续），并计入 NotificationFailureCount；
    /// 6. 非线程安全（单线程 tick / 主线程约定）。
    /// </summary>
    public sealed class ReactiveValue<T>
    {
        private readonly IEventDispatchFailureSink? _failureSink;
        private readonly List<Subscription> _subscriptions = new List<Subscription>();

        private T _value;
        private int _dispatchVersion;
        private int _dispatchDepth;
        private int _failureCount;

        /// <summary>创建可观察值（不带失败出口）。</summary>
        public ReactiveValue(T initialValue)
            : this(initialValue, null)
        {
        }

        /// <summary>创建可观察值；<paramref name="failureSink"/> 用于上报订阅者异常（可空）。</summary>
        public ReactiveValue(T initialValue, IEventDispatchFailureSink? failureSink)
        {
            _value = initialValue;
            _failureSink = failureSink;
        }

        /// <summary>当前值。写入口是 <see cref="Set"/>，以便调用方拿到"是否真的变了"。</summary>
        public T Value
        {
            get { return _value; }
        }

        /// <summary>当前活跃订阅者数量。</summary>
        public int SubscriberCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    if (_subscriptions[i].IsActive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>订阅者异常被隔离的累计次数。</summary>
        public int NotificationFailureCount
        {
            get { return _failureCount; }
        }

        /// <summary>更新值：变化则按订阅顺序通知并返回 true；未变化返回 false 且不通知。</summary>
        public bool Set(T value)
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return false;
            }

            _value = value;
            Notify(value, ++_dispatchVersion);
            return true;
        }

        /// <summary>
        /// 订阅值变化；返回的 <see cref="IDisposable"/> 用于解绑（可重复调用）。
        /// <paramref name="notifyWithCurrentValue"/> 为 true 时，订阅瞬间以当前值通知一次（View 首次渲染用）。
        /// </summary>
        public IDisposable Subscribe(Action<T> handler, bool notifyWithCurrentValue = false)
        {
            Guard.NotNull(handler, nameof(handler));

            Subscription subscription = new Subscription(this, handler);
            _subscriptions.Add(subscription);

            if (notifyWithCurrentValue)
            {
                InvokeHandler(subscription, _value);
            }

            return subscription;
        }

        private void Notify(T value, int version)
        {
            int plannedCount = _subscriptions.Count;
            _dispatchDepth++;

            try
            {
                for (int i = 0; i < plannedCount; i++)
                {
                    if (_dispatchVersion != version)
                    {
                        // 值在派发过程中又被改过：剩余订阅者交给新一轮派发，避免收到过期值。
                        return;
                    }

                    Subscription subscription = _subscriptions[i];
                    if (!subscription.IsActive)
                    {
                        continue;
                    }

                    InvokeHandler(subscription, value);
                }
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0)
                {
                    Compact();
                }
            }
        }

        private void InvokeHandler(Subscription subscription, T value)
        {
            try
            {
                subscription.Handler(value);
            }
            catch (Exception exception)
            {
                _failureCount++;

                if (_failureSink == null)
                {
                    return;
                }

                Action<T> handler = subscription.Handler;
                string handlerName = handler.Method != null
                    ? handler.Method.Name
                    : handler.ToString() ?? handler.GetType().Name;
                _failureSink.OnHandlerFailed(
                    new EventDispatchFailure(typeof(ReactiveValue<T>), handlerName, exception));
            }
        }

        private void Compact()
        {
            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                if (!_subscriptions[i].IsActive)
                {
                    _subscriptions.RemoveAt(i);
                }
            }
        }

        /// <summary>订阅句柄：解绑幂等；派发进行中只标记失效，等最外层派发结束后统一压缩。</summary>
        private sealed class Subscription : IDisposable
        {
            private readonly ReactiveValue<T> _owner;
            private readonly Action<T> _handler;

            public Subscription(ReactiveValue<T> owner, Action<T> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public Action<T> Handler
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

                if (_owner._dispatchDepth == 0)
                {
                    _owner._subscriptions.Remove(this);
                }
            }
        }
    }
}
