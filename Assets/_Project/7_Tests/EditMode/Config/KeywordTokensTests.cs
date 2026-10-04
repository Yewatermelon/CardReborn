using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T1：关键词列（Taunt|Charge）的解析与格式化契约。</summary>
    public sealed class KeywordTokensTests
    {
        [Test]
        public void TryParse_WhenSingleKeyword_SetsFlag()
        {
            bool ok = KeywordTokens.TryParse("Taunt", out Keyword keywords, out string? failedToken);

            Assert.That(ok, Is.True);
            Assert.That(keywords, Is.EqualTo(Keyword.Taunt));
            Assert.That(failedToken, Is.Null);
        }

        [Test]
        public void TryParse_WhenLowerCase_IsCaseInsensitive()
        {
            Assert.That(KeywordTokens.TryParse("taunt", out Keyword keywords, out _), Is.True);
            Assert.That(keywords, Is.EqualTo(Keyword.Taunt));
        }

        [Test]
        public void TryParse_WhenMultipleKeywords_SetsAllFlags()
        {
            Assert.That(
                KeywordTokens.TryParse("Taunt|Charge", out Keyword keywords, out _),
                Is.True);

            Assert.That(keywords.HasFlag(Keyword.Taunt), Is.True);
            Assert.That(keywords.HasFlag(Keyword.Charge), Is.True);
            Assert.That(keywords, Is.EqualTo(Keyword.Taunt | Keyword.Charge));
        }

        [Test]
        public void TryParse_WhenOrderReversed_ProducesSameFlags()
        {
            KeywordTokens.TryParse("Taunt|Charge", out Keyword first, out _);
            KeywordTokens.TryParse("Charge|Taunt", out Keyword second, out _);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void TryParse_WhenWhitespaceAroundTokens_IsTolerated()
        {
            Assert.That(
                KeywordTokens.TryParse(" Taunt | Charge ", out Keyword keywords, out _),
                Is.True);

            Assert.That(keywords, Is.EqualTo(Keyword.Taunt | Keyword.Charge));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TryParse_WhenEmpty_YieldsNone(string? text)
        {
            bool ok = KeywordTokens.TryParse(text, out Keyword keywords, out string? failedToken);

            Assert.That(ok, Is.True, "空列表示无关键词，属合法配置");
            Assert.That(keywords, Is.EqualTo(Keyword.None));
            Assert.That(failedToken, Is.Null);
        }

        [Test]
        public void TryParse_WhenUnknownToken_FailsAndReportsToken()
        {
            bool ok = KeywordTokens.TryParse("Fly", out Keyword keywords, out string? failedToken);

            Assert.That(ok, Is.False);
            Assert.That(keywords, Is.EqualTo(Keyword.None));
            Assert.That(failedToken, Is.EqualTo("Fly"));
        }

        [Test]
        public void TryParse_WhenMixedWithUnknown_FailsAndReportsOffender()
        {
            bool ok = KeywordTokens.TryParse("Taunt|Fly|Charge", out _, out string? failedToken);

            Assert.That(ok, Is.False);
            Assert.That(failedToken, Is.EqualTo("Fly"));
        }

        [Test]
        public void TryParse_WhenNumericToken_Fails()
        {
            Assert.That(KeywordTokens.TryParse("1", out _, out string? failedToken), Is.False);
            Assert.That(failedToken, Is.EqualTo("1"), "配置表禁止用数字代替枚举名");
        }

        [Test]
        public void TryParse_WhenDuplicateTokens_IsIdempotent()
        {
            Assert.That(KeywordTokens.TryParse("Taunt|Taunt", out Keyword keywords, out _), Is.True);
            Assert.That(keywords, Is.EqualTo(Keyword.Taunt));
        }

        [Test]
        public void TryParse_WhenTrailingSeparator_IsTolerated()
        {
            Assert.That(KeywordTokens.TryParse("Taunt|", out Keyword keywords, out _), Is.True);
            Assert.That(keywords, Is.EqualTo(Keyword.Taunt));
        }

        [Test]
        public void Format_WhenNone_IsEmpty()
        {
            Assert.That(KeywordTokens.Format(Keyword.None), Is.Empty);
        }

        [Test]
        public void Format_WhenMultiple_UsesStableDeclarationOrder()
        {
            string text = KeywordTokens.Format(Keyword.Charge | Keyword.Taunt);

            Assert.That(text, Is.EqualTo("Taunt|Charge"), "固定顺序保证生成物稳定，便于 diff");
        }

        [Test]
        public void Format_ThenTryParse_IsLossless()
        {
            Keyword[] samples =
            {
                Keyword.None,
                Keyword.Taunt,
                Keyword.Taunt | Keyword.Charge,
                Keyword.DivineShield | Keyword.Deathrattle | Keyword.Lifesteal,
                Keyword.Taunt | Keyword.Charge | Keyword.DivineShield | Keyword.Battlecry
                    | Keyword.Deathrattle | Keyword.Windfury | Keyword.Stealth | Keyword.Poisonous
                    | Keyword.Frozen | Keyword.Rush | Keyword.SpellPower | Keyword.Lifesteal
            };

            for (int i = 0; i < samples.Length; i++)
            {
                string text = KeywordTokens.Format(samples[i]);
                bool ok = KeywordTokens.TryParse(text, out Keyword parsed, out string? failedToken);

                Assert.That(ok, Is.True, "样本 " + i + " 应可解析，失败 token=" + failedToken);
                Assert.That(parsed, Is.EqualTo(samples[i]), "样本 " + i + " 往返应一致");
            }
        }

        [Test]
        public void TryParse_WhenAllKeywords_AllFlagsSet()
        {
            string text = KeywordTokens.Format(
                Keyword.Taunt | Keyword.Charge | Keyword.DivineShield | Keyword.Battlecry
                | Keyword.Deathrattle | Keyword.Windfury | Keyword.Stealth | Keyword.Poisonous
                | Keyword.Frozen | Keyword.Rush | Keyword.SpellPower | Keyword.Lifesteal);

            KeywordTokens.TryParse(text, out Keyword parsed, out _);

            Assert.That(System.Enum.GetValues(typeof(Keyword)).Length, Is.EqualTo(13), "1 个 None + 12 个关键词");
            Assert.That(KeywordTokens.Format(parsed), Is.EqualTo(text));
        }
    }
}
