using System;
using System.Collections.Generic;
using System.Text;
using Card.Core;

namespace Card.Domain.Config
{
    /// <summary>
    /// 加载生成物的结果：成功时给出配置集合，失败时给出全部错误（文件名 + 原因）。
    /// 放在 Domain：它是纯数据结果（无 I/O、无 Unity），读盘实现由 Infrastructure 提供。
    /// </summary>
    public sealed class ConfigLoadResult
    {
        private readonly List<string> _errors;

        private ConfigLoadResult(bool succeeded, ConfigBundle? bundle, IReadOnlyList<string> errors)
        {
            Succeeded = succeeded;
            Bundle = succeeded ? bundle : null;
            _errors = new List<string>(Guard.NotNull(errors, nameof(errors)));
        }

        public bool Succeeded { get; }

        /// <summary>成功时的配置集合；失败时为 null。</summary>
        public ConfigBundle? Bundle { get; }

        public IReadOnlyList<string> Errors
        {
            get { return _errors; }
        }

        public static ConfigLoadResult Success(ConfigBundle bundle)
        {
            return new ConfigLoadResult(true, Guard.NotNull(bundle, nameof(bundle)), Array.Empty<string>());
        }

        public static ConfigLoadResult Failure(IReadOnlyList<string> errors)
        {
            return new ConfigLoadResult(false, null, errors);
        }

        public string ToText()
        {
            if (Succeeded)
            {
                return "配置加载成功。";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("配置加载失败：共 ").Append(_errors.Count).Append(" 个问题");
            for (int i = 0; i < _errors.Count; i++)
            {
                builder.Append('\n').Append("- ").Append(_errors[i]);
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
