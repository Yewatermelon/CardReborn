using System.Collections.Generic;
using System.Text;
using Card.Core;
using Card.Domain.Config;

namespace Card.Infrastructure.Config
{
    /// <summary>一次导入的结果：是否成功、校验报告、写出的文件列表。</summary>
    public sealed class ConfigImportResult
    {
        private readonly List<string> _writtenFiles;

        private ConfigImportResult(
            bool succeeded,
            ConfigValidationReport report,
            IReadOnlyList<string> writtenFiles)
        {
            Succeeded = succeeded;
            Report = Guard.NotNull(report, nameof(report));
            _writtenFiles = new List<string>(Guard.NotNull(writtenFiles, nameof(writtenFiles)));
        }

        public bool Succeeded { get; }

        /// <summary>校验报告（失败时含全部问题）。</summary>
        public ConfigValidationReport Report { get; }

        /// <summary>成功时写出的文件绝对路径（失败时为空）。</summary>
        public IReadOnlyList<string> WrittenFiles
        {
            get { return _writtenFiles; }
        }

        public static ConfigImportResult Failure(ConfigValidationReport report)
        {
            return new ConfigImportResult(false, report, System.Array.Empty<string>());
        }

        public static ConfigImportResult Success(
            ConfigValidationReport report,
            IReadOnlyList<string> writtenFiles)
        {
            return new ConfigImportResult(true, report, writtenFiles);
        }

        /// <summary>给对话框与日志直接展示的文本。</summary>
        public string ToText()
        {
            if (!Succeeded)
            {
                return "导入失败，未写出任何文件。\n" + Report.ToText();
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("导入成功，写出 ").Append(_writtenFiles.Count).Append(" 个文件：");
            for (int i = 0; i < _writtenFiles.Count; i++)
            {
                builder.Append('\n').Append("- ").Append(_writtenFiles[i]);
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
