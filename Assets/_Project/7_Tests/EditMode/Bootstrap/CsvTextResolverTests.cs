using System;
using Card.Bootstrap.Battle;
using NUnit.Framework;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>M6-T4：最小中文文本表解析器。</summary>
    [TestFixture]
    public sealed class CsvTextResolverTests
    {
        [Test]
        public void Resolve_KnownKey_ReturnsText()
        {
            var resolver = new CsvTextResolver("Key,ZhCn\nCARD_001_NAME,胖滚\nHERO_001_NAME,法师\n");

            Assert.That(resolver.Resolve("CARD_001_NAME"), Is.EqualTo("胖滚"));
            Assert.That(resolver.Resolve("HERO_001_NAME"), Is.EqualTo("法师"));
            Assert.That(resolver.Count, Is.EqualTo(2));
        }

        [Test]
        public void Resolve_UnknownKey_FallsBackToKeyItself()
        {
            var resolver = new CsvTextResolver("Key,ZhCn\nA,甲\n");

            Assert.That(resolver.Resolve("MISSING_KEY"), Is.EqualTo("MISSING_KEY"));
        }

        [Test]
        public void Resolve_NullOrEmptyKey_ReturnsEmpty()
        {
            var resolver = new CsvTextResolver("Key,ZhCn\nA,甲\n");

            Assert.That(resolver.Resolve(null!), Is.EqualTo(string.Empty));
            Assert.That(resolver.Resolve(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Resolve_EmptyTranslation_FallsBackToKey()
        {
            var resolver = new CsvTextResolver("Key,ZhCn\nCARD_001_DESC,\n");

            Assert.That(resolver.Resolve("CARD_001_DESC"), Is.EqualTo("CARD_001_DESC"));
        }

        [Test]
        public void Ctor_DuplicateKey_LastOneWins()
        {
            var resolver = new CsvTextResolver("Key,ZhCn\nA,甲\nA,乙\n");

            Assert.That(resolver.Resolve("A"), Is.EqualTo("乙"));
            Assert.That(resolver.Count, Is.EqualTo(1));
        }

        [Test]
        public void Ctor_BlankKeyRow_IsSkipped()
        {
            var resolver = new CsvTextResolver("Key,ZhCn\n  ,无主文本\nA,甲\n");

            Assert.That(resolver.Count, Is.EqualTo(1));
        }

        [Test]
        public void Ctor_MissingZhCnColumn_Throws()
        {
            Assert.That(() => new CsvTextResolver("Key,Other\nA,甲\n"),
                Throws.InstanceOf<FormatException>());
        }

        [Test]
        public void Ctor_NullText_Throws()
        {
            Assert.That(() => new CsvTextResolver(null!),
                Throws.InstanceOf<ArgumentNullException>());
        }

        [Test]
        public void Ctor_AcceptsUtf8Bom()
        {
            var resolver = new CsvTextResolver("\uFEFFKey,ZhCn\nA,甲\n");

            Assert.That(resolver.Resolve("A"), Is.EqualTo("甲"));
        }
    }
}
