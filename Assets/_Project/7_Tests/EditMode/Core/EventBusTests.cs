using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T2：`EventBus` 的订阅、发布、解绑、契约与异常隔离。</summary>
    public sealed class EventBusTests
    {
        [Test]
        public void Subscribe_WhenPublishMatchingEvent_InvokesHandlerWithSameEvent()
        {
            EventBus bus = new EventBus();
            TestDamageEvent received = default;
            int calls = 0;
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                calls++;
                received = evt;
            });

            bus.Publish(new TestDamageEvent(7));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(received.Amount, Is.EqualTo(7));
        }

        [Test]
        public void Publish_WhenNoSubscribers_ReturnsEmptyReport()
        {
            EventBus bus = new EventBus();

            PublishReport report = bus.Publish(new TestDamageEvent(1));

            Assert.That(report.HandlerCount, Is.EqualTo(0));
            Assert.That(report.FailureCount, Is.EqualTo(0));
            Assert.That(report.HasFailures, Is.False);
        }

        [Test]
        public void Publish_WhenOtherEventType_DoesNotInvokeSubscribers()
        {
            EventBus bus = new EventBus();
            int damageCalls = 0;
            int turnCalls = 0;
            bus.Subscribe<TestDamageEvent>(evt => damageCalls++);
            bus.Subscribe<TestTurnEvent>(evt => turnCalls++);

            bus.Publish(new TestDamageEvent(3));
            Assert.That(damageCalls, Is.EqualTo(1));
            Assert.That(turnCalls, Is.EqualTo(0));

            bus.Publish(new TestTurnEvent("player-1"));
            Assert.That(damageCalls, Is.EqualTo(1));
            Assert.That(turnCalls, Is.EqualTo(1));
        }

        [Test]
        public void Publish_WhenMultipleHandlers_InvokesInSubscriptionOrder()
        {
            EventBus bus = new EventBus();
            List<string> order = new List<string>();
            bus.Subscribe<TestDamageEvent>(evt => order.Add("first"));
            bus.Subscribe<TestDamageEvent>(evt => order.Add("second"));
            bus.Subscribe<TestDamageEvent>(evt => order.Add("third"));

            bus.Publish(new TestDamageEvent(1));

            Assert.That(order, Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void Subscribe_WhenHandlerIsNull_ThrowsArgumentNullException()
        {
            EventBus bus = new EventBus();

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => bus.Subscribe<TestDamageEvent>(null));

            Assert.That(exception.ParamName, Is.EqualTo("handler"));
        }

        [Test]
        public void Publish_WhenReferenceTypeEventIsNull_ThrowsArgumentNullException()
        {
            EventBus bus = new EventBus();

            Assert.Throws<ArgumentNullException>(() => bus.Publish<TestTurnEvent>(null));
        }

        [Test]
        public void Publish_WhenValueTypeEventIsDefault_InvokesHandler()
        {
            EventBus bus = new EventBus();
            int calls = 0;
            bus.Subscribe<TestDamageEvent>(evt => calls++);

            bus.Publish(default(TestDamageEvent));

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_WhenCalledAfterSubscribe_StopsDelivery()
        {
            EventBus bus = new EventBus();
            int calls = 0;
            IDisposable token = bus.Subscribe<TestDamageEvent>(evt => calls++);

            bus.Publish(new TestDamageEvent(1));
            token.Dispose();
            bus.Publish(new TestDamageEvent(2));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(0));
        }

        [Test]
        public void Dispose_WhenCalledTwice_IsIdempotent()
        {
            EventBus bus = new EventBus();
            int calls = 0;
            int otherCalls = 0;
            IDisposable token = bus.Subscribe<TestDamageEvent>(evt => calls++);
            bus.Subscribe<TestDamageEvent>(evt => otherCalls++);

            token.Dispose();

            Assert.DoesNotThrow(() => token.Dispose());
            bus.Publish(new TestDamageEvent(1));
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(otherCalls, Is.EqualTo(1));
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_WhenSameHandlerTwice_TokensAreIndependent()
        {
            EventBus bus = new EventBus();
            int calls = 0;
            Action<TestDamageEvent> handler = evt => calls++;
            IDisposable first = bus.Subscribe(handler);
            IDisposable second = bus.Subscribe(handler);

            bus.Publish(new TestDamageEvent(1));
            Assert.That(calls, Is.EqualTo(2));

            first.Dispose();
            bus.Publish(new TestDamageEvent(2));
            Assert.That(calls, Is.EqualTo(3));

            second.Dispose();
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(0));
        }

        [Test]
        public void SubscriberCount_WhenSubscribedAndDisposed_CountsActiveOnly()
        {
            EventBus bus = new EventBus();
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(0));

            IDisposable first = bus.Subscribe<TestDamageEvent>(evt => { });
            IDisposable second = bus.Subscribe<TestDamageEvent>(evt => { });
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(2));

            second.Dispose();
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(1));

            first.Dispose();
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(0));
        }

        [Test]
        public void Publish_WhenHandlerThrows_IsolatesFailureAndContinues()
        {
            EventBus bus = new EventBus();
            List<string> order = new List<string>();
            bus.Subscribe<TestDamageEvent>(evt => order.Add("before"));
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                order.Add("throwing");
                throw new InvalidOperationException("订阅者故意抛异常");
            });
            bus.Subscribe<TestDamageEvent>(evt => order.Add("after"));

            PublishReport report = bus.Publish(new TestDamageEvent(1));

            Assert.That(order, Is.EqualTo(new[] { "before", "throwing", "after" }));
            Assert.That(report.HandlerCount, Is.EqualTo(3));
            Assert.That(report.FailureCount, Is.EqualTo(1));
            Assert.That(report.HasFailures, Is.True);
        }

        [Test]
        public void Publish_WhenHandlerThrows_ReportsFailureToSinkAndKeepsSubscription()
        {
            RecordingFailureSink sink = new RecordingFailureSink();
            EventBus bus = new EventBus(sink);
            InvalidOperationException failure = new InvalidOperationException("boom");
            bus.Subscribe<TestDamageEvent>(evt => throw failure);

            bus.Publish(new TestDamageEvent(1));
            bus.Publish(new TestDamageEvent(2));

            Assert.That(sink.Failures.Count, Is.EqualTo(2), "异常隔离不等于自动解绑");
            Assert.That(sink.Failures[0].EventType, Is.EqualTo(typeof(TestDamageEvent)));
            Assert.That(sink.Failures[0].Exception, Is.SameAs(failure));
            Assert.That(sink.Failures[0].HandlerName, Is.Not.Null.And.Not.Empty);
            Assert.That(sink.Failures[0].ToString(), Does.Contain("TestDamageEvent"));
        }

        [Test]
        public void Publish_WhenHandlerThrowsWithoutSink_OnlyReportsFailureCount()
        {
            EventBus bus = new EventBus();
            bus.Subscribe<TestDamageEvent>(evt => throw new InvalidOperationException("boom"));

            PublishReport report = default;
            Assert.DoesNotThrow(() => { report = bus.Publish(new TestDamageEvent(1)); });

            Assert.That(report.FailureCount, Is.EqualTo(1));
        }

        [Test]
        public void Publish_WhenTwoHandlersThrow_CountsBothFailures()
        {
            EventBus bus = new EventBus();
            int survivorCalls = 0;
            bus.Subscribe<TestDamageEvent>(evt => throw new InvalidOperationException("first"));
            bus.Subscribe<TestDamageEvent>(evt => survivorCalls++);
            bus.Subscribe<TestDamageEvent>(evt => throw new InvalidOperationException("second"));

            PublishReport report = bus.Publish(new TestDamageEvent(1));

            Assert.That(report.HandlerCount, Is.EqualTo(3));
            Assert.That(report.FailureCount, Is.EqualTo(2));
            Assert.That(survivorCalls, Is.EqualTo(1));
        }

        [Test]
        public void Bus_WhenTwoInstances_DoNotShareSubscriptions()
        {
            EventBus first = new EventBus();
            EventBus second = new EventBus();
            int firstCalls = 0;
            first.Subscribe<TestDamageEvent>(evt => firstCalls++);

            second.Publish(new TestDamageEvent(1));

            Assert.That(firstCalls, Is.EqualTo(0));
            Assert.That(second.SubscriberCount<TestDamageEvent>(), Is.EqualTo(0));
        }
    }
}
