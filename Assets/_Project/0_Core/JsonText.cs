using System.Globalization;
using System.Text;

namespace Card.Core
{
    /// <summary>JSON 文本细节（缩进与转义），被 <see cref="JsonValue"/> 复用，便于各自保持短小。</summary>
    internal static class JsonText
    {
        private const string Indent = "  ";

        /// <summary>按深度追加 2 空格缩进。</summary>
        public static void AppendIndent(StringBuilder builder, int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                builder.Append(Indent);
            }
        }

        /// <summary>把字符串写成 JSON 字面量：只转义必需字符，非 ASCII（含中文）原样保留。</summary>
        public static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
