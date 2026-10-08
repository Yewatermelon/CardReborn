using System;
using System.Collections.Generic;
using Card.Core;
using Card.Presentation.Battle;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 基于 <see cref="CsvTable"/> 的最小文本解析器（M6-T4；M8 正式本地化前的过渡方案）：
    /// CSV 表头固定 <c>Key,ZhCn</c>；重复 Key 后写覆盖；查无译文回退 key 本身。
    /// 纯 C#（只依赖 Core），文件读取由 <see cref="BattleSceneBootstrap"/> 负责。
    /// </summary>
    public sealed class CsvTextResolver : ITextResolver
    {
        private readonly Dictionary<string, string> _texts;

        public CsvTextResolver(string csvText)
        {
            Guard.NotNull(csvText, nameof(csvText));

            CsvTable table = CsvTable.Parse(csvText);
            int keyIndex = IndexOfColumnOrThrow(table, "Key");
            int textIndex = IndexOfColumnOrThrow(table, "ZhCn");

            _texts = new Dictionary<string, string>(table.RowCount);
            for (int i = 0; i < table.RowCount; i++)
            {
                IReadOnlyList<string> row = table.GetRow(i);
                string key = row[keyIndex];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                // 重复 Key：后写覆盖（Excel 源表手滑时以最后一条为准，不静默丢数据）。
                _texts[key.Trim()] = row[textIndex] ?? string.Empty;
            }
        }

        /// <summary>词条数量（测试/诊断用）。</summary>
        public int Count => _texts.Count;

        public string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return _texts.TryGetValue(key, out string? text) && text.Length > 0 ? text : key;
        }

        private static int IndexOfColumnOrThrow(CsvTable table, string column)
        {
            int index = -1;
            for (int i = 0; i < table.ColumnCount; i++)
            {
                if (table.Header[i] == column)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                throw new FormatException("本地化 CSV 缺少列：" + column + "（表头需为 Key,ZhCn）。");
            }

            return index;
        }
    }
}
