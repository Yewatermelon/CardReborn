namespace Card.Core
{
    /// <summary>
    /// 日志通道（Docs/03 第 8 节）：按模块开关与过滤，便于发布版本按需静音。
    /// 新增通道时同步更新 <see cref="GameLog"/> 的默认开关表。
    /// </summary>
    public enum LogChannel
    {
        /// <summary>启动与生命周期。</summary>
        Boot = 0,

        /// <summary>配置加载与校验。</summary>
        Config = 1,

        /// <summary>对局流程。</summary>
        Match = 2,

        /// <summary>规则校验与结算。</summary>
        Rule = 3,

        /// <summary>AI 决策。</summary>
        Ai = 4,

        /// <summary>存档与读取。</summary>
        Save = 5,

        /// <summary>界面与交互。</summary>
        Ui = 6,

        /// <summary>性能度量。</summary>
        Perf = 7
    }
}
