using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T3：`StateMachine<TState>` 的注册、转移校验与查询。</summary>
    public sealed class StateMachineTests
    {
        [Test]
        public void Ctor_WhenCreated_CurrentIsInitialState()
        {
            List<string> log = new List<string>();

            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            Assert.That(machine.Current, Is.EqualTo(TestPhase.Idle));
        }

        [Test]
        public void Ctor_WhenInitialHandlerOmitted_DoesNotRegisterInitialState()
        {
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);

            Assert.That(machine.IsRegistered(TestPhase.Idle), Is.False, "只传初始状态不注册任何状态");

            machine.Register(TestPhase.Idle);
            Assert.That(machine.IsRegistered(TestPhase.Idle), Is.True);
        }

        [Test]
        public void Register_WhenSameStateTwice_ThrowsArgumentException()
        {
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(TestPhase.Running);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => machine.Register(TestPhase.Running))!;

            Assert.That(exception.ParamName, Is.EqualTo("state"));
        }

        [Test]
        public void AllowTransition_WhenFromStateUnregistered_ThrowsArgumentException()
        {
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(TestPhase.Running);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => machine.AllowTransition(TestPhase.Idle, TestPhase.Running))!;

            Assert.That(exception.ParamName, Is.EqualTo("from"));
        }

        [Test]
        public void AllowTransition_WhenToStateUnregistered_ThrowsArgumentException()
        {
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(TestPhase.Idle);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => machine.AllowTransition(TestPhase.Idle, TestPhase.Running))!;

            Assert.That(exception.ParamName, Is.EqualTo("to"));
        }

        [Test]
        public void TransitionTo_WhenAllowed_ReturnsSuccessAndUpdatesCurrent()
        {
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(new List<string>());

            Result result = machine.TransitionTo(TestPhase.Running);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Running));
        }

        [Test]
        public void TransitionTo_WhenTargetNotRegisteredAtAll_ReturnsUnknownStateFailure()
        {
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);

            Result result = machine.TransitionTo(TestPhase.Paused);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo(StateMachine<TestPhase>.ErrorUnknownState));
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Idle));
        }

        [Test]
        public void TransitionTo_WhenEdgeNotDeclared_ReturnsIllegalTransitionFailure()
        {
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(new List<string>());

            Result result = machine.TransitionTo(TestPhase.Paused);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo(StateMachine<TestPhase>.ErrorIllegalTransition));
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Idle));
        }

        [Test]
        public void TransitionTo_WhenSelfTransitionNotDeclared_IsRejected()
        {
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(new List<string>());

            Result result = machine.TransitionTo(TestPhase.Idle);

            Assert.That(result.ErrorCode, Is.EqualTo(StateMachine<TestPhase>.ErrorIllegalTransition));
        }

        [Test]
        public void CanTransitionTo_ReflectsDeclaredEdgesWithoutSideEffects()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            Assert.That(machine.CanTransitionTo(TestPhase.Running), Is.True);
            Assert.That(machine.CanTransitionTo(TestPhase.Paused), Is.False, "未声明边");
            Assert.That(machine.CanTransitionTo(TestPhase.Stopped), Is.False, "未注册状态优先返回 false");
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Idle));
            Assert.That(log, Is.Empty, "查询不应触发任何回调");
        }

        [Test]
        public void AllowTransition_WhenDeclaredTwice_IsIdempotent()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            Assert.DoesNotThrow(() => machine.AllowTransition(TestPhase.Idle, TestPhase.Running));
            Result result = machine.TransitionTo(TestPhase.Running);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle", "Enter:Running" }));
        }

        [Test]
        public void TransitionTo_WhenChainedWithLoopBack_ReachesFinalState()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            machine.TransitionTo(TestPhase.Running);
            machine.TransitionTo(TestPhase.Paused);
            machine.TransitionTo(TestPhase.Running);
            Result result = machine.TransitionTo(TestPhase.Stopped);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Stopped));
            Assert.That(log, Is.EqualTo(new[]
            {
                "Exit:Idle", "Enter:Running",
                "Exit:Running", "Enter:Paused",
                "Exit:Paused", "Enter:Running",
                "Exit:Running", "Enter:Stopped"
            }));
        }

        [Test]
        public void TransitionTo_WhenFailedThenLegal_RecoversCleanly()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            Result rejected = machine.TransitionTo(TestPhase.Paused);
            Result accepted = machine.TransitionTo(TestPhase.Running);

            Assert.That(rejected.IsFailure, Is.True);
            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Running));
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle", "Enter:Running" }));
        }

        [Test]
        public void Machines_WhenTwoInstances_AreIndependent()
        {
            StateMachine<TestPhase> first = StateMachineTestFactory.CreateBasic(new List<string>());
            StateMachine<TestPhase> second = new StateMachine<TestPhase>(TestPhase.Idle);

            first.TransitionTo(TestPhase.Running);

            Assert.That(second.Current, Is.EqualTo(TestPhase.Idle));
            Assert.That(second.IsRegistered(TestPhase.Running), Is.False);
            Assert.That(second.CanTransitionTo(TestPhase.Running), Is.False);
        }
    }
}
