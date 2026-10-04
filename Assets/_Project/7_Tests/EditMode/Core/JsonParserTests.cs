using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M2-T5：JSON 解析器（读取方向），与 JsonValue 的写出方向互为逆运算。</summary>
    public sealed class JsonParserTests
    {
        [Test]
        public void Parse_WhenScalars_ParsesEveryKind()
        {
            Assert.That(JsonValue.Parse("42").IntValue, Is.EqualTo(42));
            Assert.That(JsonValue.Parse("-7").IntValue, Is.EqualTo(-7));
            Assert.That(JsonValue.Parse("true").BoolValue, Is.True);
            Assert.That(JsonValue.Parse("false").BoolValue, Is.False);
            Assert.That(JsonValue.Parse("null").Kind, Is.EqualTo(JsonKind.Null));
            Assert.That(JsonValue.Parse("\"ALPHA\"").StringValue, Is.EqualTo("ALPHA"));
        }

        [Test]
        public void Parse_WhenWhitespaceEverywhere_IsTolerated()
        {
            JsonValue value = JsonValue.Parse("  {\n  \"a\" : [ 1 , 2 ]\t}\r\n");

            Assert.That(value.TryGetMember("a", out JsonValue items), Is.True);
            Assert.That(items.Items.Count, Is.EqualTo(2));
            Assert.That(items.Items[1].IntValue, Is.EqualTo(2));
        }

        [Test]
        public void Parse_WhenNested_BuildsTree()
        {
            JsonValue value = JsonValue.Parse("{\"a\":{\"b\":[{\"c\":1}]}}");

            value.TryGetMember("a", out JsonValue a);
            a.TryGetMember("b", out JsonValue b);
            b.Items[0].TryGetMember("c", out JsonValue c);
            Assert.That(c.IntValue, Is.EqualTo(1));
        }

        [Test]
        public void Parse_WhenStringHasEscapes_UnescapesThem()
        {
            JsonValue value = JsonValue.Parse("\"a\\\"b\\\\c\\nd\\te\\u4e2d\"");

            Assert.That(value.StringValue, Is.EqualTo("a\"b\\c\nd\te中"));
        }

        [Test]
        public void Parse_WhenStringHasChinese_KeepsItReadable()
        {
            Assert.That(JsonValue.Parse("\"法师\"").StringValue, Is.EqualTo("法师"));
        }

        [Test]
        public void Parse_WhenEmptyCollections_ReturnsEmptyCollections()
        {
            Assert.That(JsonValue.Parse("{}").Members, Is.Empty);
            Assert.That(JsonValue.Parse("[]").Items, Is.Empty);
            Assert.That(JsonValue.Parse("{ }").Members, Is.Empty);
        }

        [Test]
        public void Parse_WhenBomPresent_IsTolerated()
        {
            Assert.That(JsonValue.Parse("\uFEFF{\"a\":1}").Members.Count, Is.EqualTo(1));
        }

        [Test]
        public void Parse_WhenTextIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => JsonValue.Parse(null!));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Parse_WhenTextIsEmpty_ThrowsFormatException(string text)
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse(text));
        }

        [Test]
        public void Parse_WhenTrailingContent_ThrowsWithPosition()
        {
            FormatException exception = Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\":1} x"))!;

            Assert.That(exception.Message, Does.Contain("多余内容"));
        }

        [Test]
        public void Parse_WhenObjectIsUnclosed_Throws()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\":1"));
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\":1,"));
        }

        [Test]
        public void Parse_WhenLiteralIsUnknown_Throws()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("nul"));
            Assert.Throws<FormatException>(() => JsonValue.Parse("True"));
        }

        [Test]
        public void Parse_WhenNumberIsDecimal_ThrowsBecauseOnlyIntegersAreSupported()
        {
            FormatException exception = Assert.Throws<FormatException>(() => JsonValue.Parse("1.5"))!;

            Assert.That(exception.Message, Does.Contain("只支持整数"));
        }

        [Test]
        public void Parse_WhenMemberIsDuplicated_Throws()
        {
            FormatException exception = Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\":1,\"a\":2}"))!;

            Assert.That(exception.Message, Does.Contain("重复"));
        }

        [Test]
        public void Parse_WhenColonIsMissing_Throws()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\" 1}"));
        }

        [Test]
        public void Parse_WhenStringIsUnclosed_Throws()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("\"abc"));
        }

        [Test]
        public void Parse_ThenToJson_IsStable()
        {
            JsonValue document = JsonValue.Object()
                .Add("schemaVersion", JsonValue.From(1))
                .Add("cards", JsonValue.Array(
                    JsonValue.Object()
                        .Add("id", JsonValue.From(1))
                        .Add("key", JsonValue.From("NEUTRAL_PANGO"))
                        .Add("effects", JsonValue.Array(JsonValue.From("DamageEffect:2")))));

            string text = document.ToJson();

            Assert.That(JsonValue.Parse(text).ToJson(), Is.EqualTo(text), "写出 → 解析 → 再写出必须逐字符一致");
        }
    }
}
