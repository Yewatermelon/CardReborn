using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T3 测试状态：模拟"回合阶段"这类枚举状态。</summary>
    internal enum TestPhase
    {
        Idle,
        Running,
        Paused,
        Stopped
    }

    /// <summary>把 Enter/Exit 追加到共享日志的处理器，可选在回调中抛异常。</summary>
    internal sealed class RecordingStateHandler : IStateHandler<TestPhase>
    {
        private readonly List<string> _log;
        private readonly TestPhase _phase;
        private readonly bool _throwOnEnter;
        private readonly bool _throwOnExit;

        public RecordingStateHandler(
            List<string> log,
            TestPhase phase,
            bool throwOnEnter = false,
            bool throwOnExit = false)
        {
            _log = log;
            _phase = phase;
            _throwOnEnter = throwOnEnter;
            _throwOnExit = throwOnExit;
        }

        public void Enter()
        {
            _log.Add("Enter:" + _phase);
            if (_throwOnEnter)
            {
                throw new InvalidOperationException("enter failed: " + _phase);
            }
        }

        public void Exit()
        {
            _log.Add("Exit:" + _phase);
            if (_throwOnExit)
            {
                throw new InvalidOperationException("exit failed: " + _phase);
            }
        }
    }

    /// <summary>M1-T3 共用的状态机装配：Idle → Running ⇄ Paused，Running → Stopped。</summary>
    internal static class StateMachineTestFactory
    {
        public static StateMachine<TestPhase> CreateBasic(List<string> log)
        {
            StateMachine<TestPhase> machine = new StateMachine<TestPhase>(
                TestPhase.Idle,
                new RecordingStateHandler(log, TestPhase.Idle));
            machine.Register(TestPhase.Running, new RecordingStateHandler(log, TestPhase.Running));
            machine.Register(TestPhase.Paused, new RecordingStateHandler(log, TestPhase.Paused));
            machine.Register(TestPhase.Stopped, new RecordingStateHandler(log, TestPhase.Stopped));
            machine.AllowTransition(TestPhase.Idle, TestPhase.Running);
            machine.AllowTransition(TestPhase.Running, TestPhase.Paused);
            machine.AllowTransition(TestPhase.Paused, TestPhase.Running);
            machine.AllowTransition(TestPhase.Running, TestPhase.Stopped);
            return machine;
        }
    }
}
