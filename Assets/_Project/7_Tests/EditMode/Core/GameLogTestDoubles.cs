using System;
using System.Collections.Generic;
using Card.Core;

namespace Card.Tests.EditMode.Core
{
    /// <summary>一条被记录下来的日志。</summary>
    internal sealed class LogEntry
    {
        public LogEntry(LogLevel level, LogChannel channel, string message)
        {
            Level = level;
            Channel = channel;
            Message = message;
        }

        public LogLevel Level { get; }

        public LogChannel Channel { get; }

        public string Message { get; }
    }

    /// <summary>记录型日志后端。</summary>
    internal sealed class RecordingLogSink : ILogSink
    {
        private readonly List<LogEntry> _entries = new List<LogEntry>();

        public IReadOnlyList<LogEntry> Entries
        {
            get { return _entries; }
        }

        public void Write(LogLevel level, LogChannel channel, string message)
        {
            _entries.Add(new LogEntry(level, channel, message));
        }
    }

    /// <summary>可配置"前 N 次抛异常"或"一直抛异常"的后端，用于验证日志故障不穿透。</summary>
    internal sealed class ThrowingLogSink : ILogSink
    {
        private readonly Exception _failure;
        private readonly int _failuresBeforeRecovery;
        private int _writeCount;

        public ThrowingLogSink(Exception failure, int failuresBeforeRecovery = -1)
        {
            _failure = failure;
            _failuresBeforeRecovery = failuresBeforeRecovery;
        }

        public int WriteCount
        {
            get { return _writeCount; }
        }

        public void Write(LogLevel level, LogChannel channel, string message)
        {
            _writeCount++;
            if (_failuresBeforeRecovery < 0 || _writeCount <= _failuresBeforeRecovery)
            {
                throw _failure;
            }
        }
    }

    /// <summary>GameLog 是静态门面，测试之间必须显式重置状态。</summary>
    internal static class GameLogTestHelper
    {
        public static void Reset()
        {
            GameLog.Disable();
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                GameLog.SetChannelEnabled(channel, true);
            }
        }
    }
}
