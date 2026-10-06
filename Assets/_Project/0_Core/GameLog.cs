using System;

namespace Card.Core
{
    /// <summary>
    /// 静态日志门面（Docs/03 第 8 节）：分通道、分等级、可开关，且不依赖任何 Unity API，
    /// 因此客户端与服务端（Unity 工程外）可共用同一份内核源码。
    ///
    /// 使用约定：
    /// 1. 组合根调用一次 <see cref="Configure"/> 注入后端；<b>未配置时所有日志调用都是安全的空操作</b>；
    /// 2. 规则层只调用 <c>Trace/Info/Warn/Error</c>；配置能力只属于组合根（本类型不是服务查找器）；
    /// 3. 热路径先问 <see cref="IsEnabled"/> 再决定是否拼接字符串（03 第 8 节规则 5）；
    /// 4. 被过滤的日志连参数校验都不做，以保证过滤路径零开销；
    /// 5. 非线程安全（单线程 tick / 主线程约定，与 EventBus / StateMachine / 随机源一致）。
    /// </summary>
    public static class GameLog
    {
        private static readonly bool[] s_channelEnabled = CreateChannelTable();

        private static ILogSink? s_sink;
        private static LogLevel s_minimumLevel = LogLevel.Info;
        private static int s_sinkFailureCount;
        private static Exception? s_lastSinkFailure;

        /// <summary>当前最小等级：低于它的日志被过滤。</summary>
        public static LogLevel MinimumLevel
        {
            get { return s_minimumLevel; }
        }

        /// <summary>后端抛异常的累计次数（只增不减，便于诊断"日志静默失效"）。</summary>
        public static int SinkFailureCount
        {
            get { return s_sinkFailureCount; }
        }

        /// <summary>最近一次后端异常；从未失败时为 null。</summary>
        public static Exception? LastSinkFailure
        {
            get { return s_lastSinkFailure; }
        }

        /// <summary>注入日志后端与最小等级（组合根一次性调用）。发布版本通常传 <see cref="LogLevel.Warn"/>。</summary>
        public static void Configure(ILogSink sink, LogLevel minimumLevel)
        {
            Guard.NotNull(sink, nameof(sink));

            s_sink = sink;
            s_minimumLevel = minimumLevel;
        }

        /// <summary>关闭输出（等价于未配置）：用于未接线场景与测试隔离。</summary>
        public static void Disable()
        {
            s_sink = null;
        }

        /// <summary>
        /// 测试隔离用：清空后端、重置最小等级为 Info、归零失败计数与最近异常。
        /// 生产代码不应调用。
        /// </summary>
        internal static void ResetForTests()
        {
            s_sink = null;
            s_minimumLevel = LogLevel.Info;
            s_sinkFailureCount = 0;
            s_lastSinkFailure = null;
        }

        /// <summary>枚举值是否是已定义的通道。</summary>
        public static bool IsDefinedChannel(LogChannel channel)
        {
            return Enum.IsDefined(typeof(LogChannel), channel);
        }

        /// <summary>按模块开关通道；传未定义的通道值视为调用方错误。</summary>
        public static void SetChannelEnabled(LogChannel channel, bool enabled)
        {
            if (!IsDefinedChannel(channel))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channel),
                    channel,
                    "未定义的日志通道。");
            }

            s_channelEnabled[(int)channel] = enabled;
        }

        /// <summary>通道是否打开；未定义的通道值返回 true（宁可多输出，也不静默丢日志）。</summary>
        public static bool IsChannelEnabled(LogChannel channel)
        {
            int index = (int)channel;
            if (index < 0 || index >= s_channelEnabled.Length)
            {
                return true;
            }

            return s_channelEnabled[index];
        }

        /// <summary>当前配置下该等级 + 通道是否会真正输出（供热路径提前短路）。</summary>
        public static bool IsEnabled(LogChannel channel, LogLevel level)
        {
            return level >= s_minimumLevel && IsChannelEnabled(channel);
        }

        /// <summary>调试级日志（默认关闭）。</summary>
        public static void Trace(LogChannel channel, string message)
        {
            Write(LogLevel.Trace, channel, message);
        }

        /// <summary>常规信息。</summary>
        public static void Info(LogChannel channel, string message)
        {
            Write(LogLevel.Info, channel, message);
        }

        /// <summary>警告。</summary>
        public static void Warn(LogChannel channel, string message)
        {
            Write(LogLevel.Warn, channel, message);
        }

        /// <summary>错误；错误日志必须可定位（带上卡牌 Key / 玩家 Id / 命令类型，见 03 第 8 节规则 3）。</summary>
        public static void Error(LogChannel channel, string message, Exception? exception = null)
        {
            Write(LogLevel.Error, channel, message, exception);
        }

        /// <summary>通用写入口：被过滤时直接返回（不校验参数、不拼接字符串）。</summary>
        public static void Write(LogLevel level, LogChannel channel, string message, Exception? exception = null)
        {
            if (!IsEnabled(channel, level))
            {
                return;
            }

            Guard.NotNullOrWhiteSpace(message, nameof(message));

            ILogSink? sink = s_sink;
            if (sink == null)
            {
                return;
            }

            string text = exception == null ? message : message + Environment.NewLine + exception;

            try
            {
                sink.Write(level, channel, text);
            }
            catch (Exception sinkFailure)
            {
                // 日志后端故障不得穿透游戏循环（Docs/03 第 11.2 节）。
                // 这里不是"静默吞异常"：失败被计数并留档，可用于诊断与测试断言。
                s_sinkFailureCount++;
                s_lastSinkFailure = sinkFailure;
            }
        }

        private static bool[] CreateChannelTable()
        {
            bool[] table = new bool[Enum.GetValues(typeof(LogChannel)).Length];
            for (int i = 0; i < table.Length; i++)
            {
                table[i] = true;
            }

            return table;
        }
    }
}
