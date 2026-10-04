using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M2-T3：极简 JSON 文档模型的构造与确定性序列化。</summary>
    public sealed class JsonValueTests
    {
        [Test]
        public void From_WhenScalars_WritesJsonLiterals()
        {
            Assert.That(JsonValue.From(42).ToJson(), Is.EqualTo("42"));
            Assert.That(JsonValue.From(-7).ToJson(), Is.EqualTo("-7"));
            Assert.That(JsonValue.From(true).ToJson(), Is.EqualTo("true"));
            Assert.That(JsonValue.From(false).ToJson(), Is.EqualTo("false"));
            Assert.That(JsonValue.Null().ToJson(), Is.EqualTo("null"));
        }

        [Test]
        public void From_WhenString_WrapsInQuotes()
        {
            Assert.That(JsonValue.From("ALPHA").ToJson(), Is.EqualTo("\"ALPHA\""));
        }

        [Test]
        public void From_WhenStringIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => JsonValue.From(null!))!;

            Assert.That(exception.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void ToJson_WhenStringHasSpecialCharacters_EscapesThem()
        {
            string text = JsonValue.From("a\"b\\c\nd\te\u0001f").ToJson();

            Assert.That(text, Is.EqualTo("\"a\\\"b\\\\c\\nd\\te\\u0001f\""));
        }

        [Test]
        public void ToJson_WhenStringHasChinese_KeepsItReadable()
        {
            Assert.That(JsonValue.From("法师").ToJson(), Is.EqualTo("\"法师\""), "非 ASCII 不转义，便于人工核对");
        }

        [Test]
        public void ToJson_WhenEmptyCollections_WritesBrackets()
        {
            Assert.That(JsonValue.Object().ToJson(), Is.EqualTo("{}"));
            Assert.That(JsonValue.Array().ToJson(), Is.EqualTo("[]"));
        }

        [Test]
        public void ToJson_WhenNested_UsesTwoSpaceIndentAndFixedOrder()
        {
            JsonValue document = JsonValue.Object()
                .Add("schemaVersion", JsonValue.From(1))
                .Add("cards", JsonValue.Array(
                    JsonValue.Object()
                        .Add("id", JsonValue.From(1))
                        .Add("effects", JsonValue.Array(JsonValue.From("DamageEffect:2")))));

            string expected = string.Join("\n", new[]
            {
                "{",
                "  \"schemaVersion\": 1,",
                "  \"cards\": [",
                "    {",
                "      \"id\": 1,",
                "      \"effects\": [",
                "        \"DamageEffect:2\"",
                "      ]",
                "    }",
                "  ]",
                "}"
            });

            Assert.That(document.ToJson(), Is.EqualTo(expected));
        }

        [Test]
        public void Add_WhenMemberDuplicated_KeepsFirstPositionAndReplacesValue()
        {
            JsonValue document = JsonValue.Object()
                .Add("a", JsonValue.From(1))
                .Add("b", JsonValue.From(2))
                .Add("a", JsonValue.From(3));

            Assert.That(document.ToJson(), Is.EqualTo("{\n  \"a\": 3,\n  \"b\": 2\n}"));
            Assert.That(document.Members.Count, Is.EqualTo(2));
        }

        [Test]
        public void Add_WhenNameIsNull_ThrowsArgumentNullException()
        {
            JsonValue document = JsonValue.Object();

            Assert.Throws<ArgumentNullException>(() => document.Add(null!, JsonValue.From(1)));
        }

        [Test]
        public void Add_WhenValueIsNull_ThrowsArgumentNullException()
        {
            JsonValue document = JsonValue.Object();

            Assert.Throws<ArgumentNullException>(() => document.Add("a", null!));
        }

        [Test]
        public void Add_WhenTargetIsNotObject_ThrowsInvalidOperationException()
        {
            JsonValue array = JsonValue.Array();

            Assert.Throws<InvalidOperationException>(() => array.Add("a", JsonValue.From(1)));
        }

        [Test]
        public void Array_WhenItemIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => JsonValue.Array(JsonValue.From(1), null!));
        }

        [Test]
        public void TryGetMember_ReflectsMembership()
        {
            JsonValue document = JsonValue.Object().Add("id", JsonValue.From(7));

            Assert.That(document.TryGetMember("id", out JsonValue id), Is.True);
            Assert.That(id.IntValue, Is.EqualTo(7));
            Assert.That(document.TryGetMember("nope", out _), Is.False);
        }

        [Test]
        public void Kind_And_Payloads_ReflectConstruction()
        {
            Assert.That(JsonValue.From(5).Kind, Is.EqualTo(JsonKind.Number));
            Assert.That(JsonValue.From(5).IntValue, Is.EqualTo(5));
            Assert.That(JsonValue.From(false).Kind, Is.EqualTo(JsonKind.Bool));
            Assert.That(JsonValue.From(false).BoolValue, Is.False);
            Assert.That(JsonValue.From("x").Kind, Is.EqualTo(JsonKind.String));
            Assert.That(JsonValue.From("x").StringValue, Is.EqualTo("x"));
            Assert.That(JsonValue.Null().Kind, Is.EqualTo(JsonKind.Null));
            Assert.That(JsonValue.Array().Items, Is.Empty);
            Assert.That(JsonValue.Object().Members, Is.Empty);
        }

        [Test]
        public void ToString_WhenCalled_EqualsToJson()
        {
            JsonValue document = JsonValue.Object().Add("id", JsonValue.From(1));

            Assert.That(document.ToString(), Is.EqualTo(document.ToJson()));
        }
    }
}
