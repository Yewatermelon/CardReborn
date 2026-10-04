namespace Card.Domain.Config
{
    /// <summary>一条配置校验问题：表名 / 行号（0 表示整表问题）/ 列名（空表示整表问题）/ 原因。</summary>
    public sealed class ConfigValidationIssue
    {
        public ConfigValidationIssue(string tableName, int lineNumber, string columnName, string message)
        {
            TableName = tableName;
            LineNumber = lineNumber;
            ColumnName = columnName;
            Message = message;
        }

        public string TableName { get; }

        public int LineNumber { get; }

        public string ColumnName { get; }

        public string Message { get; }

        public override string ToString()
        {
            if (LineNumber <= 0)
            {
                return ColumnName.Length == 0
                    ? TableName + "：" + Message
                    : TableName + " [" + ColumnName + "]：" + Message;
            }

            return TableName + " 第 " + LineNumber + " 行 [" + ColumnName + "]：" + Message;
        }
    }
}
