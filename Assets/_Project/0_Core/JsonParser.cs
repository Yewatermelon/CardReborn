using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Card.Core
{
    /// <summary>
    /// 极简 JSON 解析器（与 <see cref="JsonValue"/> 配套）。只覆盖配置生成物用到的子集：
    /// 对象 / 数组 / 字符串 / **整数** / 布尔 / null。刻意严格：
    /// 多余结尾内容、未闭合结构、未知字面量、小数、重复键、尾随逗号一律报错并给**位置**。
    /// 按 Docs/03 第 5.9.4 节，读写都在 BCL 层自己实现（Unity 无 System.Text.Json）。
    /// </summary>
    internal static class JsonParser
    {
        public static JsonValue Parse(string text)
        {
            Guard.NotNull(text, nameof(text));

            string source = text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
            int index = 0;
            JsonValue value = ParseValue(source, ref index);
            SkipWhitespace(source, ref index);

            if (index != source.Length)
            {
                throw Error(index, "解析结束后仍有多余内容");
            }

            return value;
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                throw Error(index, "内容意外结束");
            }

            char c = text[index];
            switch (c)
            {
                case '{':
                    return ParseObject(text, ref index);
                case '[':
                    return ParseArray(text, ref index);
                case '"':
                    return JsonValue.From(ParseString(text, ref index));
                case 't':
                    Expect(text, ref index, "true");
                    return JsonValue.From(true);
                case 'f':
                    Expect(text, ref index, "false");
                    return JsonValue.From(false);
                case 'n':
                    Expect(text, ref index, "null");
                    return JsonValue.Null();
                default:
                    return JsonValue.From(ParseInt(text, ref index));
            }
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            index++; // '{'
            JsonValue result = JsonValue.Object();
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == '}')
            {
                index++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                string name = ParseString(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length || text[index] != ':')
                {
                    throw Error(index, "对象成员缺少 ':'");
                }

                index++;
                JsonValue value = ParseValue(text, ref index);

                if (result.TryGetMember(name, out _))
                {
                    throw Error(index, "对象成员重复：" + name);
                }

                result.Add(name, value);
                SkipWhitespace(text, ref index);

                if (index < text.Length && text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (index < text.Length && text[index] == '}')
                {
                    index++;
                    return result;
                }

                throw Error(index, "对象成员后缺少 ',' 或 '}'");
            }
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            index++; // '['
            List<JsonValue> items = new List<JsonValue>();
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == ']')
            {
                index++;
                return JsonValue.Array(items.ToArray());
            }

            while (true)
            {
                items.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index < text.Length && text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (index < text.Length && text[index] == ']')
                {
                    index++;
                    return JsonValue.Array(items.ToArray());
                }

                throw Error(index, "数组元素后缺少 ',' 或 ']'");
            }
        }

        private static string ParseString(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length || text[index] != '"')
            {
                throw Error(index, "期望字符串");
            }

            index++;
            StringBuilder builder = new StringBuilder();

            while (true)
            {
                if (index >= text.Length)
                {
                    throw Error(index, "字符串未闭合");
                }

                char c = text[index++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c < ' ')
                {
                    throw Error(index - 1, "字符串里不能出现裸控制字符");
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                builder.Append(ParseEscape(text, ref index));
            }
        }

        private static char ParseEscape(string text, ref int index)
        {
            if (index >= text.Length)
            {
                throw Error(index, "转义序列未完成");
            }

            char escape = text[index++];
            switch (escape)
            {
                case '"': return '"';
                case '\\': return '\\';
                case '/': return '/';
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'u': return ParseUnicode(text, ref index);
                default: throw Error(index - 1, "未知转义：\\" + escape);
            }
        }

        private static char ParseUnicode(string text, ref int index)
        {
            if (index + 4 > text.Length)
            {
                throw Error(index, "\\u 转义需要 4 位十六进制");
            }

            string hex = text.Substring(index, 4);
            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
            {
                throw Error(index, "\\u 转义不是合法十六进制：" + hex);
            }

            index += 4;
            return (char)code;
        }

        private static int ParseInt(string text, ref int index)
        {
            int start = index;

            if (index < text.Length && (text[index] == '-' || text[index] == '+'))
            {
                index++;
            }

            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            if (index == start)
            {
                throw Error(start, "无法识别的取值");
            }

            string token = text.Substring(start, index - start);
            if (index < text.Length && (text[index] == '.' || text[index] == 'e' || text[index] == 'E'))
            {
                throw Error(index, "配置 JSON 目前只支持整数，实际为 '" + token + "…'");
            }

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw Error(start, "整数超出 int 范围：" + token);
            }

            return value;
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw Error(index, "期望字面量 " + literal);
            }

            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                {
                    return;
                }

                index++;
            }
        }

        private static FormatException Error(int index, string message)
        {
            return new FormatException("JSON 解析失败（位置 " + index + "）：" + message);
        }
    }
}
