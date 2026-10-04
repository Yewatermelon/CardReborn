using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T7：`ReactiveValue<T>` 的值语义、订阅与解绑。</summary>
    public sealed class ReactiveValueTests
    {
        [Test]
        public void Ctor_WhenCreated_ExposesInitialValueAndNoSubscribers()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(42);

            Assert.That(value.Value, Is.EqualTo(42));
            Assert.That(value.SubscriberCount, Is.EqualTo(0));
            Assert.That(value.NotificationFailureCount, Is.EqualTo(0));
        }

        [Test]
        public void Set_WhenValueChanges_NotifiesSubscriberWithNewValue()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(1);
            int received = 0;
            value.Subscribe(v => received = v);

            bool changed = value.Set(5);

            Assert.That(changed, Is.True);
            Assert.That(received, Is.EqualTo(5));
            Assert.That(value.Value, Is.EqualTo(5));
        }

        [Test]
        public void Set_WhenValueIsEqual_DoesNotNotifyAndReturnsFalse()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(1);
            int calls = 0;
            value.Subscribe(v => calls++);

            bool changed = value.Set(1);

            Assert.That(changed, Is.False);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Set_WhenStringValueIsEqual_DoesNotNotify()
        {
            ReactiveValue<string> value = new ReactiveValue<string>("KEY_A");
            int calls = 0;
            value.Subscribe(v => calls++);

            bool changed = value.Set("KEY_A");

            Assert.That(changed, Is.False);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Set_WhenReferenceValueBecomesNull_IsTreatedAsChange()
        {
            ReactiveValue<string> value = new ReactiveValue<string>("KEY_A");
            string received = "unset";
            value.Subscribe(v => received = v);

            bool changed = value.Set(null);

            Assert.That(changed, Is.True);
            Assert.That(received, Is.Null);
            Assert.That(value.Value, Is.Null);
        }

        [Test]
        public void Set_WhenStructValueChanges_Notifies()
        {
            ReactiveValue<TestDamageEvent> value = new ReactiveValue<TestDamageEvent>(new TestDamageEvent(1));
            TestDamageEvent received = default;
            value.Subscribe(v => received = v);

            bool changed = value.Set(new TestDamageEvent(3));

            Assert.That(changed, Is.True);
            Assert.That(received.Amount, Is.EqualTo(3));
        }

        [Test]
        public void Set_WhenStructValueIsEqual_DoesNotNotify()
        {
            ReactiveValue<TestDamageEvent> value = new ReactiveValue<TestDamageEvent>(new TestDamageEvent(2));
            int calls = 0;
            value.Subscribe(v => calls++);

            bool changed = value.Set(new TestDamageEvent(2));

            Assert.That(changed, Is.False);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Subscribe_WhenCalled_DoesNotNotifyImmediately()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(9);
            int calls = 0;

            value.Subscribe(v => calls++);

            Assert.That(calls, Is.EqualTo(0), "默认只在值变化时通知");
        }

        [Test]
        public void Subscribe_WhenNotifyWithCurrentValue_InvokesHandlerImmediately()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(9);
            int received = 0;

            value.Subscribe(v => received = v, notifyWithCurrentValue: true);

            Assert.That(received, Is.EqualTo(9));
        }

        [Test]
        public void Subscribe_WhenHandlerIsNull_ThrowsArgumentNullException()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);

            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => value.Subscribe(null));

            Assert.That(exception.ParamName, Is.EqualTo("handler"));
        }

        [Test]
        public void Dispose_WhenCalled_StopsNotifications()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            int calls = 0;
            IDisposable token = value.Subscribe(v => calls++);

            value.Set(1);
            token.Dispose();
            value.Set(2);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(value.SubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_WhenCalledTwice_IsIdempotent()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            int calls = 0;
            IDisposable token = value.Subscribe(v => calls++);

            token.Dispose();

            Assert.DoesNotThrow(() => token.Dispose());
            value.Set(1);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Subscribe_WhenSameHandlerTwice_TokensAreIndependent()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            int calls = 0;
            Action<int> handler = v => calls++;
            IDisposable first = value.Subscribe(handler);
            value.Subscribe(handler);

            value.Set(1);
            Assert.That(calls, Is.EqualTo(2));

            first.Dispose();
            value.Set(2);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void SubscriberCount_ReflectsActiveSubscriptions()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);
            IDisposable first = value.Subscribe(v => { });
            IDisposable second = value.Subscribe(v => { });

            Assert.That(value.SubscriberCount, Is.EqualTo(2));

            second.Dispose();
            Assert.That(value.SubscriberCount, Is.EqualTo(1));

            first.Dispose();
            Assert.That(value.SubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void Set_WhenNoSubscribers_UpdatesValueWithoutThrowing()
        {
            ReactiveValue<int> value = new ReactiveValue<int>(0);

            Assert.DoesNotThrow(() => value.Set(3));
            Assert.That(value.Value, Is.EqualTo(3));
        }

        [Test]
        public void Values_WhenTwoInstances_AreIndependent()
        {
            ReactiveValue<int> first = new ReactiveValue<int>(1);
            ReactiveValue<int> second = new ReactiveValue<int>(1);
            int firstCalls = 0;
            first.Subscribe(v => firstCalls++);

            second.Set(2);

            Assert.That(firstCalls, Is.EqualTo(0));
            Assert.That(first.Value, Is.EqualTo(1));
            Assert.That(second.Value, Is.EqualTo(2));
        }
    }
}
