using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T3：Enter/Exit 回调顺序、无处理器状态、回调抛异常的行为。</summary>
    public sealed class StateMachineCallbackTests
    {
        [Test]
        public void TransitionTo_WhenAllowed_ExitsPreviousThenEntersNext()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            machine.TransitionTo(TestPhase.Running);

            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle", "Enter:Running" }));
        }

        [Test]
        public void TransitionTo_WhenTargetHasNoHandler_StillSucceeds()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(TestPhase.Idle, new RecordingStateHandler(log, TestPhase.Idle));
            machine.Register(TestPhase.Running);
            machine.AllowTransition(TestPhase.Idle, TestPhase.Running);

            Result result = machine.TransitionTo(TestPhase.Running);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Running));
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle" }));
        }

        [Test]
        public void TransitionTo_WhenSelfTransitionDeclared_ExitsThenEntersSameState()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(TestPhase.Idle, new RecordingStateHandler(log, TestPhase.Idle));
            machine.AllowTransition(TestPhase.Idle, TestPhase.Idle);

            Result result = machine.TransitionTo(TestPhase.Idle);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle", "Enter:Idle" }));
        }

        [Test]
        public void TransitionTo_WhenIllegal_DoesNotInvokeAnyHandler()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = StateMachineTestFactory.CreateBasic(log);

            machine.TransitionTo(TestPhase.Stopped);

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void TransitionTo_WhenExitThrows_PropagatesAndTargetEnterIsNotCalled()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(
                TestPhase.Idle,
                new RecordingStateHandler(log, TestPhase.Idle, throwOnExit: true));
            machine.Register(TestPhase.Running, new RecordingStateHandler(log, TestPhase.Running));
            machine.AllowTransition(TestPhase.Idle, TestPhase.Running);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => machine.TransitionTo(TestPhase.Running));

            Assert.That(exception.Message, Does.Contain("exit failed"));
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle" }), "Exit 抛异常后不应再执行 Enter");
            Assert.That(
                machine.Current,
                Is.EqualTo(TestPhase.Running),
                "异常向上传播（不吞异常），且状态指针已是目标状态");
        }

        [Test]
        public void TransitionTo_WhenEnterThrows_PropagatesAndKeepsTargetState()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(TestPhase.Idle, new RecordingStateHandler(log, TestPhase.Idle));
            machine.Register(
                TestPhase.Running,
                new RecordingStateHandler(log, TestPhase.Running, throwOnEnter: true));
            machine.AllowTransition(TestPhase.Idle, TestPhase.Running);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => machine.TransitionTo(TestPhase.Running));

            Assert.That(exception.Message, Does.Contain("enter failed"));
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle", "Enter:Running" }));
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Running));
        }

        [Test]
        public void TransitionTo_WhenHandlerThrowsOnce_SubsequentTransitionsStillWork()
        {
            List<string> log = new List<string>();
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(TestPhase.Idle);
            machine.Register(
                TestPhase.Idle,
                new RecordingStateHandler(log, TestPhase.Idle, throwOnExit: true));
            machine.Register(TestPhase.Running, new RecordingStateHandler(log, TestPhase.Running));
            machine.Register(TestPhase.Paused, new RecordingStateHandler(log, TestPhase.Paused));
            machine.AllowTransition(TestPhase.Idle, TestPhase.Running);
            machine.AllowTransition(TestPhase.Running, TestPhase.Paused);

            Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(TestPhase.Running));
            Result result = machine.TransitionTo(TestPhase.Paused);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(machine.Current, Is.EqualTo(TestPhase.Paused));
            Assert.That(log, Is.EqualTo(new[] { "Exit:Idle", "Exit:Running", "Enter:Paused" }));
        }
    }
}
