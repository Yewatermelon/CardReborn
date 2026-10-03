using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M1-T2 共享测试替身：两个事件类型（值类型 / 引用类型）与一个记录型失败出口。</summary>
    internal readonly struct TestDamageEvent
    {
        public TestDamageEvent(int amount)
        {
            Amount = amount;
        }

        public int Amount { get; }
    }

    /// <summary>引用类型事件：用于验证"引用类型事件不可为 null"的契约。</summary>
    internal sealed class TestTurnEvent
    {
        public TestTurnEvent(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }

    /// <summary>记录所有被隔离异常的失败出口。</summary>
    internal sealed class RecordingFailureSink : IEventDispatchFailureSink
    {
        private readonly List<EventDispatchFailure> _failures = new List<EventDispatchFailure>();

        public IReadOnlyList<EventDispatchFailure> Failures
        {
            get { return _failures; }
        }

        public void OnHandlerFailed(EventDispatchFailure failure)
        {
            _failures.Add(failure);
        }
    }
}
