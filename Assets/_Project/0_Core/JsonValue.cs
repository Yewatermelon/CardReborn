using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Card.Core
{
    /// <summary>
    /// 极简 JSON 文档模型：构造 + 确定性序列化。
    /// 为什么自己写：Unity 2022.3 的 BCL 不含 System.Text.Json（实测 CS0234），
    /// 而 Docs/03 第 5.9.4 节允许"自定义 BCL 实现"；这样服务端与客户端共用同一份代码，零新增依赖。
    ///
    /// 输出约定（生成物要能被 git diff，所以格式固定）：
    /// 1. 缩进 2 空格、换行 LF、成员顺序 = 首次加入顺序、对象成员重复时覆盖但保留首次位置；
    /// 2. 空对象 <c>{}</c>、空数组 <c>[]</c>；
    /// 3. 字符串只转义必需的字符（引号、反斜杠、控制字符），中文等非 ASCII 原样保留；
    /// 4. ToJson() 不追加结尾换行（写文件时由调用方决定）。
    ///
    /// 读取方向（解析）尚未实现：M2-T5 的 CardDatabase 需要时补，避免两套模型。
    /// </summary>
    public sealed class JsonValue
    {
        private static readonly List<JsonValue> NoItems = new List<JsonValue>();
        private static readonly List<KeyValuePair<string, JsonValue>> NoMembers =
            new List<KeyValuePair<string, JsonValue>>();

        private readonly JsonKind _kind;
        private readonly bool _boolValue;
        private readonly int _intValue;
        private readonly string _stringValue;
        private readonly List<JsonValue> _items;
        private readonly List<KeyValuePair<string, JsonValue>> _members;

        private JsonValue(
            JsonKind kind,
            bool boolValue,
            int intValue,
            string stringValue,
            List<JsonValue> items,
            List<KeyValuePair<string, JsonValue>> members)
        {
            _kind = kind;
            _boolValue = boolValue;
            _intValue = intValue;
            _stringValue = stringValue;
            _items = items;
            _members = members;
        }

        /// <summary>取值种类。</summary>
        public JsonKind Kind
        {
            get { return _kind; }
        }

        public bool BoolValue
        {
            get { return _boolValue; }
        }

        public int IntValue
        {
            get { return _intValue; }
        }

        public string StringValue
        {
            get { return _stringValue; }
        }

        /// <summary>数组元素（非数组时为空集合）。</summary>
        public IReadOnlyList<JsonValue> Items
        {
            get { return _items; }
        }

        /// <summary>对象成员（非对象时为空集合）。</summary>
        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members
        {
            get { return _members; }
        }

        public static JsonValue Null()
        {
            return new JsonValue(JsonKind.Null, false, 0, string.Empty, NoItems, NoMembers);
        }

        public static JsonValue From(bool value)
        {
            return new JsonValue(JsonKind.Bool, value, 0, string.Empty, NoItems, NoMembers);
        }

        public static JsonValue From(int value)
        {
            return new JsonValue(JsonKind.Number, false, value, string.Empty, NoItems, NoMembers);
        }

        public static JsonValue From(string value)
        {
            Guard.NotNull(value, nameof(value));
            return new JsonValue(JsonKind.String, false, 0, value, NoItems, NoMembers);
        }

        public static JsonValue Array(params JsonValue[] items)
        {
            Guard.NotNull(items, nameof(items));

            List<JsonValue> list = new List<JsonValue>(items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                Guard.NotNull(items[i], nameof(items));
                list.Add(items[i]);
            }

            return new JsonValue(JsonKind.Array, false, 0, string.Empty, list, NoMembers);
        }

        public static JsonValue Object()
        {
            return new JsonValue(
                JsonKind.Object,
                false,
                0,
                string.Empty,
                NoItems,
                new List<KeyValuePair<string, JsonValue>>());
        }

        /// <summary>添加对象成员（仅对象可用）；同名覆盖但保留首次出现的位置。</summary>
        public JsonValue Add(string name, JsonValue value)
        {
            Guard.NotNull(name, nameof(name));
            Guard.NotNull(value, nameof(value));

            if (_kind != JsonKind.Object)
            {
                throw new InvalidOperationException("只有 JSON 对象才能添加成员。");
            }

            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].Key, name, StringComparison.Ordinal))
                {
                    _members[i] = new KeyValuePair<string, JsonValue>(name, value);
                    return this;
                }
            }

            _members.Add(new KeyValuePair<string, JsonValue>(name, value));
            return this;
        }

        /// <summary>按名字取成员；不存在返回 false。</summary>
        public bool TryGetMember(string name, out JsonValue value)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].Key, name, StringComparison.Ordinal))
                {
                    value = _members[i].Value;
                    return true;
                }
            }

            value = Null();
            return false;
        }

        /// <summary>确定性序列化（2 空格缩进、LF、无结尾换行）。</summary>
        public string ToJson()
        {
            StringBuilder builder = new StringBuilder();
            Write(builder, 0);
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToJson();
        }

        private void Write(StringBuilder builder, int depth)
        {
            switch (_kind)
            {
                case JsonKind.Null:
                    builder.Append("null");
                    return;
                case JsonKind.Bool:
                    builder.Append(_boolValue ? "true" : "false");
                    return;
                case JsonKind.Number:
                    builder.Append(_intValue.ToString(CultureInfo.InvariantCulture));
                    return;
                case JsonKind.String:
                    JsonText.AppendString(builder, _stringValue);
                    return;
                case JsonKind.Array:
                    WriteArray(builder, depth);
                    return;
                default:
                    WriteObject(builder, depth);
                    return;
            }
        }

        private void WriteArray(StringBuilder builder, int depth)
        {
            if (_items.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            builder.Append('[');
            for (int i = 0; i < _items.Count; i++)
            {
                builder.Append('\n');
                JsonText.AppendIndent(builder, depth + 1);
                _items[i].Write(builder, depth + 1);
                if (i < _items.Count - 1)
                {
                    builder.Append(',');
                }
            }

            builder.Append('\n');
            JsonText.AppendIndent(builder, depth);
            builder.Append(']');
        }

        private void WriteObject(StringBuilder builder, int depth)
        {
            if (_members.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append('{');
            for (int i = 0; i < _members.Count; i++)
            {
                builder.Append('\n');
                JsonText.AppendIndent(builder, depth + 1);
                JsonText.AppendString(builder, _members[i].Key);
                builder.Append(": ");
                _members[i].Value.Write(builder, depth + 1);
                if (i < _members.Count - 1)
                {
                    builder.Append(',');
                }
            }

            builder.Append('\n');
            JsonText.AppendIndent(builder, depth);
            builder.Append('}');
        }

    }
}
