using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>
    /// M1-T2：派发期间变更订阅集合、重入发布的语义。
    /// 依据 Docs/03 第 5.4 节：订阅可解绑、异常隔离、事件顺序与结算顺序一致。
    /// </summary>
    public sealed class EventBusDispatchRuleTests
    {
        [Test]
        public void Publish_WhenHandlerDisposesLaterSubscription_SkipsItInCurrentDispatch()
        {
            EventBus bus = new EventBus();
            int lateCalls = 0;
            IDisposable? lateToken = null;
            bus.Subscribe<TestDamageEvent>(evt => lateToken!.Dispose());
            lateToken = bus.Subscribe<TestDamageEvent>(evt => lateCalls++);

            bus.Publish(new TestDamageEvent(1));

            Assert.That(lateCalls, Is.EqualTo(0), "派发中已被解绑且尚未调用的订阅者应被跳过");
        }

        [Test]
        public void Publish_WhenHandlerDisposesItself_DoesNotAffectOtherSubscribers()
        {
            EventBus bus = new EventBus();
            int selfCalls = 0;
            int otherCalls = 0;
            IDisposable? selfToken = null;
            selfToken = bus.Subscribe<TestDamageEvent>(evt =>
            {
                selfCalls++;
                selfToken!.Dispose();
            });
            bus.Subscribe<TestDamageEvent>(evt => otherCalls++);

            bus.Publish(new TestDamageEvent(1));
            Assert.That(selfCalls, Is.EqualTo(1));
            Assert.That(otherCalls, Is.EqualTo(1));

            bus.Publish(new TestDamageEvent(2));
            Assert.That(selfCalls, Is.EqualTo(1));
            Assert.That(otherCalls, Is.EqualTo(2));
        }

        [Test]
        public void Publish_WhenHandlerSubscribesDuringDispatch_NewSubscriberWaitsForNextPublish()
        {
            EventBus bus = new EventBus();
            int lateCalls = 0;
            bool alreadySubscribed = false;
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                if (alreadySubscribed)
                {
                    return;
                }

                alreadySubscribed = true;
                bus.Subscribe<TestDamageEvent>(inner => lateCalls++);
            });

            bus.Publish(new TestDamageEvent(1));
            Assert.That(lateCalls, Is.EqualTo(0), "派发期间新增的订阅不参与本次派发");

            bus.Publish(new TestDamageEvent(2));
            Assert.That(lateCalls, Is.EqualTo(1));
        }

        [Test]
        public void Publish_WhenHandlerPublishesSameEventType_ReentrantDispatchCompletes()
        {
            EventBus bus = new EventBus();
            int outerCalls = 0;
            int innerCalls = 0;
            bool reentered = false;
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                outerCalls++;
                if (reentered)
                {
                    return;
                }

                reentered = true;
                bus.Publish(new TestDamageEvent(99));
            });
            bus.Subscribe<TestDamageEvent>(evt => innerCalls++);

            bus.Publish(new TestDamageEvent(1));

            Assert.That(outerCalls, Is.EqualTo(2), "外层与重入的内层各派发一次");
            Assert.That(innerCalls, Is.EqualTo(2));
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(2));
        }

        [Test]
        public void Publish_WhenHandlerPublishesOtherEventType_BothDispatchesComplete()
        {
            EventBus bus = new EventBus();
            int damageCalls = 0;
            int turnCalls = 0;
            bus.Subscribe<TestTurnEvent>(evt => turnCalls++);
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                damageCalls++;
                bus.Publish(new TestTurnEvent("player-1"));
            });

            bus.Publish(new TestDamageEvent(1));

            Assert.That(damageCalls, Is.EqualTo(1));
            Assert.That(turnCalls, Is.EqualTo(1));
        }

        [Test]
        public void Publish_WhenHandlersThrowAndDispose_KeepsRemainingOrderIntact()
        {
            EventBus bus = new EventBus();
            string log = string.Empty;
            IDisposable? third = null;
            bus.Subscribe<TestDamageEvent>(evt => log += "a");
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                log += "b";
                third!.Dispose();
            });
            third = bus.Subscribe<TestDamageEvent>(evt => log += "c");
            bus.Subscribe<TestDamageEvent>(evt =>
            {
                log += "d";
                throw new InvalidOperationException("last");
            });

            PublishReport report = bus.Publish(new TestDamageEvent(1));

            Assert.That(log, Is.EqualTo("abd"));
            Assert.That(report.HandlerCount, Is.EqualTo(3));
            Assert.That(report.FailureCount, Is.EqualTo(1));
            Assert.That(bus.SubscriberCount<TestDamageEvent>(), Is.EqualTo(3));
        }
    }
}
