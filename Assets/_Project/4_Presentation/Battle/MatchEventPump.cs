using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 事件泵（M5-T2）：观察权威侧事件流（<see cref="MatchController"/> 的
    /// <c>Events</c>，追加式只读列表）的增量，把新事件按序派发给订阅者。
    /// 构造时跳过既有历史——视图首帧直接用快照数据渲染，泵只负责增量刷新。
    /// 纯 C#，不含 UnityEngine 依赖。
    /// </summary>
    public sealed class MatchEventPump
    {
        private readonly IReadOnlyList<GameEvent> _source;
        private int _nextIndex;

        public MatchEventPump(IReadOnlyList<GameEvent> source)
        {
            _source = Guard.NotNull(source, nameof(source));
            _nextIndex = source.Count;
        }

        /// <summary>每个新事件按追加顺序派发一次。</summary>
        public event Action<GameEvent>? EventAppended;

        /// <summary>已追加但尚未派发的事件条数。</summary>
        public int PendingCount => _source.Count - _nextIndex;

        /// <summary>派发全部新事件，返回本次派发条数。</summary>
        public int Pump()
        {
            int dispatched = 0;
            while (_nextIndex < _source.Count)
            {
                EventAppended?.Invoke(_source[_nextIndex]);
                _nextIndex++;
                dispatched++;
            }

            return dispatched;
        }
    }
}
