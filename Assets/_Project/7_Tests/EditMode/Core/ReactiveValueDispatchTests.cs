using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T7：通知顺序、异常隔离与"派发期间变更订阅/值"的语义。</summary>
    public sealed class ReactiveValueDispatchTests
    {
        [Test]
        public void Set_WhenMultipleSubscribers_NotifiesInSubscriptionOrder()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            List<string> order = new List<string>();
            value.Subscribe(v => order.Add("first"));
            value.Subscribe(v => order.Add("second"));
            value.Subscribe(v => order.Add("third"));

            value.Set(1);

            Assert.That(order, Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void Set_WhenHandlerThrows_IsolatesFailureAndContinues()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            List<string> order = new List<string>();
            value.Subscribe(v => order.Add("before"));
            value.Subscribe(v => throw new InvalidOperationException("订阅者故意抛异常"));
            value.Subscribe(v => order.Add("after"));

            value.Set(1);

            Assert.That(order, Is.EqualTo(new[] { "before", "after" }));
            Assert.That(value.NotificationFailureCount, Is.EqualTo(1));
            Assert.That(value.Value, Is.EqualTo(1), "订阅者异常不影响值更新");
        }

        [Test]
        public void Set_WhenHandlerThrows_ReportsFailureToSink()
        {
            RecordingFailureSink sink = new RecordingFailureSink();
            ReactiveValue<int> value = new ReactiveValue<int>(0, sink);
            InvalidOperationException failure = new InvalidOperationException("boom");
            value.Subscribe(v => throw failure);

            value.Set(1);

            Assert.That(sink.Failures.Count, Is.EqualTo(1));
            Assert.That(sink.Failures[0].Exception, Is.SameAs(failure));
            Assert.That(sink.Failures[0].HandlerName, Is.Not.Null.And.Not.Empty);
            Assert.That(sink.Failures[0].ToString(), Does.Contain("ReactiveValue"));
        }

        [Test]
        public void Set_WhenNoSink_StillCountsFailures()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            value.Subscribe(v => throw new InvalidOperationException("boom"));

            Assert.DoesNotThrow(() => value.Set(1));

            Assert.That(value.NotificationFailureCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_WhenImmediateNotifyThrows_IsolatesAndKeepsSubscription()
        {
            RecordingFailureSink sink = new RecordingFailureSink();
            ReactiveValue<int> value = new ReactiveValue<int>(7, sink);

            IDisposable token = value.Subscribe(
                v => throw new InvalidOperationException("boom"),
                notifyWithCurrentValue: true);

            Assert.That(token, Is.Not.Null);
            Assert.That(value.SubscriberCount, Is.EqualTo(1));
            Assert.That(value.NotificationFailureCount, Is.EqualTo(1));
            Assert.That(sink.Failures.Count, Is.EqualTo(1));
        }

        [Test]
        public void Set_WhenHandlerSubscribesDuringDispatch_NewSubscriberWaitsForNextChange()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            int lateCalls = 0;
            bool alreadySubscribed = false;
            value.Subscribe(v =>
            {
                if (alreadySubscribed)
                {
                    return;
                }

                alreadySubscribed = true;
                value.Subscribe(inner => lateCalls++);
            });

            value.Set(1);
            Assert.That(lateCalls, Is.EqualTo(0), "派发期间新增的订阅不参与本次通知");

            value.Set(2);
            Assert.That(lateCalls, Is.EqualTo(1));
        }

        [Test]
        public void Set_WhenHandlerDisposesLaterSubscription_SkipsIt()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            int lateCalls = 0;
            IDisposable lateToken = null;
            value.Subscribe(v => lateToken.Dispose());
            lateToken = value.Subscribe(v => lateCalls++);

            value.Set(1);

            Assert.That(lateCalls, Is.EqualTo(0), "派发中已解绑且尚未通知的订阅者应被跳过");
        }

        [Test]
        public void Set_WhenHandlerSetsNewValue_RemainingHandlersSkipStaleValue()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(1);
            List<string> calls = new List<string>();
            bool reentered = false;
            value.Subscribe(v =>
            {
                calls.Add("first:" + v);
                if (reentered)
                {
                    return;
                }

                reentered = true;
                value.Set(3);
            });
            value.Subscribe(v => calls.Add("second:" + v));

            value.Set(2);

            Assert.That(calls, Is.EqualTo(new[] { "first:2", "first:3", "second:3" }));
            Assert.That(value.Value, Is.EqualTo(3));
            Assert.That(calls, Does.Not.Contain("second:2"), "过期的旧值不应再通知剩余订阅者");
        }

        [Test]
        public void Set_WhenHandlerSetsEqualValue_DoesNotRecurse()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(1);
            int firstCalls = 0;
            int secondCalls = 0;
            value.Subscribe(v =>
            {
                firstCalls++;
                value.Set(v);
            });
            value.Subscribe(v => secondCalls++);

            value.Set(2);

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(1));
        }
    }
}
