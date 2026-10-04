using System;

namespace Card.Domain.Config
{
    /// <summary>
    /// 生成物读取失败（文件损坏、字段缺失或类型不符、schema 版本不匹配）。
    /// 消息含**来源文件**与 **JSON 路径**（如 <c>cards[2].cost</c>），便于定位。
    /// </summary>
    public sealed class ConfigReadException : Exception
    {
        public ConfigReadException(string source, string jsonPath, string reason)
            : base(source + " 的 " + jsonPath + " 有问题：" + reason)
        {
            SourceName = source;
            JsonPath = jsonPath;
            Reason = reason;
        }

        /// <summary>来源文件名（如 cards.json）。刻意不叫 Source，避免与 Exception.Source 撞名。</summary>
        public string SourceName { get; }

        /// <summary>JSON 路径（如 cards[2].cost）。</summary>
        public string JsonPath { get; }

        /// <summary>失败原因。</summary>
        public string Reason { get; }
    }
}
