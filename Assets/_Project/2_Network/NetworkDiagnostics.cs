using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Network
{
    /// <summary>
    /// 网络诊断事件类型（M12-T9）。
    /// </summary>
    public enum NetworkEventType
    {
        Connected,
        Disconnected,
        HelloReceived,
        HelloAckSent,
        ReadyReceived,
        MatchStartSent,
        PingSent,
        PingReceived,
        PongSent,
        PongReceived,
        HeartbeatTimeout,
        ProtocolVersionMismatch,
        SendError,
        ReceiveFrame,
        SnapshotRequestReceived,
        SendFrame
    }

    /// <summary>
    /// 单次网络诊断事件（M12-T9）：时间戳 + 事件类型 + 详情。
    /// </summary>
    public readonly struct NetworkEventRecord
    {
        public int Tick { get; }
        public NetworkEventType EventType { get; }
        public string Detail { get; }

        public NetworkEventRecord(int tick, NetworkEventType eventType, string detail)
        {
            Tick = tick;
            EventType = eventType;
            Detail = detail;
        }
    }

    /// <summary>
    /// 网络诊断环形缓冲区（M12-T9；Docs/02 M12-T9）：存最近 N 条网络事件，可导出诊断文件。
    /// 线程安全（ConcurrentQueue 实现），独立于 GameLog——GameLog 走 sink 输出，
    /// 本类走内存缓冲供诊断导出。
    /// </summary>
    public sealed class NetworkDiagnostics
    {
        private readonly Queue<NetworkEventRecord> _ring;
        private readonly int _capacity;
        private readonly IClock _clock;

        public NetworkDiagnostics(IClock clock, int capacity = 256)
        {
            _clock = clock;
            _capacity = capacity;
            _ring = new Queue<NetworkEventRecord>(capacity);
        }

        /// <summary>已记录事件数（≤ Capacity）。</summary>
        public int Count => _ring.Count;

        /// <summary>缓冲区容量上限。</summary>
        public int Capacity => _capacity;

        /// <summary>记录一条诊断事件。</summary>
        public void Record(NetworkEventType eventType, string detail = "")
        {
            var record = new NetworkEventRecord(_clock.CurrentTick, eventType, detail);
            lock (_ring)
            {
                if (_ring.Count >= _capacity) _ring.Dequeue();
                _ring.Enqueue(record);
            }
        }

        /// <summary>导出为数组（按时间顺序，最早→最新）。</summary>
        public NetworkEventRecord[] Export()
        {
            lock (_ring)
            {
                return _ring.ToArray();
            }
        }

        /// <summary>清空缓冲区。</summary>
        public void Clear()
        {
            lock (_ring)
            {
                _ring.Clear();
            }
        }
    }

    /// <summary>
    /// 网络日志辅助（M12-T9）：同时写 GameLog + NetworkDiagnostics。
    /// </summary>
    public static class NetworkLogger
    {
        /// <summary>GameLog 通道：Card.Core.LogChannel.Network。</summary>
        public const LogChannel Channel = LogChannel.Network;

        public static void Info(string message)
        {
            if (GameLog.IsEnabled(Channel, LogLevel.Info))
                GameLog.Info(Channel, message);
        }

        public static void Warn(string message)
        {
            if (GameLog.IsEnabled(Channel, LogLevel.Warn))
                GameLog.Warn(Channel, message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            if (GameLog.IsEnabled(Channel, LogLevel.Error))
                GameLog.Error(Channel, message, ex);
        }

        public static void Trace(string message)
        {
            if (GameLog.IsEnabled(Channel, LogLevel.Trace))
                GameLog.Trace(Channel, message);
        }
    }
}
