using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 极简 CSV 表（配置源表专用）：首行表头、逗号分隔、不做引号转义。
    ///
    /// 约定（Docs/01 第 7.3 节）：
    /// 1. 首行必须是表头，且列名与代码契约一致（大小写敏感——写错就是要早暴露）；
    /// 2. 单元格内禁止出现逗号，因此无需引号/转义规则；
    /// 3. 行比表头**长**视为手滑，抛 FormatException 并给出行号；行比表头**短**则缺失单元格读作空串
    ///    （Excel 另存 CSV 的常见行为）；
    /// 4. 空行/全空白行跳过，不计入行数；UTF-8 BOM 不参与第一个列名。
    /// </summary>
    public sealed class CsvTable
    {
        private readonly List<string> _header = new List<string>();
        private readonly List<string[]> _rows = new List<string[]>();

        private CsvTable()
        {
        }

        /// <summary>表头（已去除首尾空白）。</summary>
        public IReadOnlyList<string> Header
        {
            get { return _header; }
        }

        /// <summary>列数。</summary>
        public int ColumnCount
        {
            get { return _header.Count; }
        }

        /// <summary>数据行数（不含表头与空行）。</summary>
        public int RowCount
        {
            get { return _rows.Count; }
        }

        /// <summary>解析 CSV 文本；缺少表头或行列数不匹配时抛 FormatException。</summary>
        public static CsvTable Parse(string text)
        {
            Guard.NotNull(text, nameof(text));

            CsvTable table = new CsvTable();
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool headerSeen = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = i == 0 ? lines[i].TrimStart('\uFEFF') : lines[i];

                if (line.Trim().Length == 0)
                {
                    continue;
                }

                string[] cells = line.Split(',');

                if (!headerSeen)
                {
                    table.FillHeader(cells);
                    headerSeen = true;
                    continue;
                }

                table.AddRow(cells, i + 1);
            }

            if (!headerSeen)
            {
                throw new FormatException("CSV 缺少表头行：配置表首行必须是表头。");
            }

            return table;
        }

        private void FillHeader(string[] cells)
        {
            for (int c = 0; c < cells.Length; c++)
            {
                _header.Add(cells[c].Trim());
            }

            if (_header.Count == 0)
            {
                throw new FormatException("CSV 表头为空。");
            }
        }

        private void AddRow(string[] cells, int lineNumber)
        {
            if (cells.Length > _header.Count)
            {
                throw new FormatException(
                    "第 " + lineNumber + " 行的列数(" + cells.Length + ")多于表头(" + _header.Count + ")。");
            }

            string[] row = new string[_header.Count];
            for (int c = 0; c < row.Length; c++)
            {
                row[c] = c < cells.Length ? cells[c].Trim() : string.Empty;
            }

            _rows.Add(row);
        }

        /// <summary>按行号取整行（只读视图）。</summary>
        public IReadOnlyList<string> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rows.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rowIndex),
                    rowIndex,
                    "行号越界：共 " + _rows.Count + " 行。");
            }

            return _rows[rowIndex];
        }

        /// <summary>列名是否存在（大小写敏感）。</summary>
        public bool HasColumn(string columnName)
        {
            return IndexOfColumn(columnName) >= 0;
        }

        /// <summary>列下标；不存在返回 -1。</summary>
        public int IndexOfColumn(string columnName)
        {
            Guard.NotNull(columnName, nameof(columnName));

            for (int i = 0; i < _header.Count; i++)
            {
                if (string.Equals(_header[i], columnName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>按列名取单元格；列不存在时抛 ArgumentException（表头拼错必须立刻暴露）。</summary>
        public string GetCell(int rowIndex, string columnName)
        {
            int columnIndex = IndexOfColumn(columnName);
            if (columnIndex < 0)
            {
                throw new ArgumentException("CSV 中不存在列：" + columnName, nameof(columnName));
            }

            return GetRow(rowIndex)[columnIndex];
        }
    }
}
