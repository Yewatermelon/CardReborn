using System;
using System.Collections.Generic;
using System.Text;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 校验报告：一次列出全部问题（FR-1.2）。只要有错就 <b>不</b>给出解析结果，
    /// 从而保证"不产生半成品数据"。
    /// </summary>
    public sealed class ConfigValidationReport
    {
        private readonly List<ConfigValidationIssue> _issues;

        public ConfigValidationReport(IReadOnlyList<ConfigValidationIssue> issues, ConfigBundle? bundle)
        {
            _issues = new List<ConfigValidationIssue>(Guard.NotNull(issues, nameof(issues)));
            Result = _issues.Count == 0 ? bundle : null;
        }

        /// <summary>全部问题（按发现顺序）。</summary>
        public IReadOnlyList<ConfigValidationIssue> Issues
        {
            get { return _issues; }
        }

        public bool HasErrors
        {
            get { return _issues.Count > 0; }
        }

        public int Count
        {
            get { return _issues.Count; }
        }

        /// <summary>校验通过时的解析结果；有错时为 null。</summary>
        public ConfigBundle? Result { get; }

        /// <summary>给日志与编辑器对话框直接展示的多行文本。</summary>
        public string ToText()
        {
            if (_issues.Count == 0)
            {
                return "配置校验通过。";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("配置校验失败：共 ").Append(_issues.Count).Append(" 个问题");
            for (int i = 0; i < _issues.Count; i++)
            {
                builder.Append('\n').Append("- ").Append(_issues[i]);
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
