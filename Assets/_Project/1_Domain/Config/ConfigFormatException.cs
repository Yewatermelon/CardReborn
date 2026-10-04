using System;

namespace Card.Domain.Config
{
    /// <summary>
    /// 配置行解析失败：必须能定位到**表名 / 行号 / 列名**，
    /// 对应 Docs/01 第 7.3 节第 5 条与 Docs/03 第 9.1 节第 4 条（配置错误必须有明确错误）。
    /// </summary>
    public sealed class ConfigFormatException : Exception
    {
        public ConfigFormatException(string tableName, int lineNumber, string columnName, string reason)
            : base(tableName + " 第 " + lineNumber + " 行 [" + columnName + "] " + reason)
        {
            TableName = tableName;
            LineNumber = lineNumber;
            ColumnName = columnName;
            Reason = reason;
        }

        /// <summary>表名（如 Cards.csv）。</summary>
        public string TableName { get; }

        /// <summary>源文件行号（1 起）。</summary>
        public int LineNumber { get; }

        /// <summary>出问题的列名；表头缺列时为空串。</summary>
        public string ColumnName { get; }

        /// <summary>失败原因（面向填写者的说明）。</summary>
        public string Reason { get; }
    }
}
