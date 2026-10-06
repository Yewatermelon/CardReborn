using System;
using System.Collections.Generic;
using Card.Domain.Match.Effects;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T4 AC-1：EffectParser 解析 5 种效果 + 未知类型抛异常。</summary>
    [TestFixture]
    public sealed class EffectParserTests
    {
        [Test]
        public void Parse_DamageEffect()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(new[] { "DamageEffect:3" });
            Assert.That(effects.Count, Is.EqualTo(1));
            DamageEffectData d = (DamageEffectData)effects[0];
            Assert.That(d.Amount, Is.EqualTo(3));
        }

        [Test]
        public void Parse_HealEffect()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(new[] { "HealEffect:5" });
            HealEffectData h = (HealEffectData)effects[0];
            Assert.That(h.Amount, Is.EqualTo(5));
        }

        [Test]
        public void Parse_DrawCardEffect()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(new[] { "DrawCardEffect:2" });
            DrawCardEffectData d = (DrawCardEffectData)effects[0];
            Assert.That(d.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parse_SummonEffect()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(new[] { "SummonEffect:M1,1" });
            SummonEffectData s = (SummonEffectData)effects[0];
            Assert.That(s.CardKey, Is.EqualTo("M1"));
            Assert.That(s.Count, Is.EqualTo(1));
        }

        [Test]
        public void Parse_BuffEffect()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(new[] { "BuffEffect:2,3" });
            BuffEffectData b = (BuffEffectData)effects[0];
            Assert.That(b.Attack, Is.EqualTo(2));
            Assert.That(b.Health, Is.EqualTo(3));
        }

        [Test]
        public void Parse_MultipleEffects_InOrder()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(
                new[] { "DamageEffect:1", "DrawCardEffect:1" });
            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(effects[0], Is.TypeOf<DamageEffectData>());
            Assert.That(effects[1], Is.TypeOf<DrawCardEffectData>());
        }

        [Test]
        public void Parse_UnknownType_Throws()
        {
            Assert.That(() => EffectParser.Parse(new[] { "FooEffect:1" }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Parse_EmptyList_ReturnsEmpty()
        {
            IReadOnlyList<IEffectData> effects = EffectParser.Parse(Array.Empty<string>());
            Assert.That(effects.Count, Is.EqualTo(0));
        }
    }
}
