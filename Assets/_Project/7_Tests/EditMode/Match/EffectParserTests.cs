using System;
using System.Collections.Generic;
using Card.Domain.Match.Effects;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T4 AC-1（T5 演进）：EffectParser 解析 5 种效果 + 未知类型抛异常；返回 TriggeredEffect。</summary>
    [TestFixture]
    public sealed class EffectParserTests
    {
        [Test]
        public void Parse_DamageEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "DamageEffect:3" });
            Assert.That(effects.Count, Is.EqualTo(1));
            DamageEffectData d = (DamageEffectData)effects[0].Effect;
            Assert.That(d.Amount, Is.EqualTo(3));
        }

        [Test]
        public void Parse_HealEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "HealEffect:5" });
            HealEffectData h = (HealEffectData)effects[0].Effect;
            Assert.That(h.Amount, Is.EqualTo(5));
        }

        [Test]
        public void Parse_DrawCardEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "DrawCardEffect:2" });
            DrawCardEffectData d = (DrawCardEffectData)effects[0].Effect;
            Assert.That(d.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parse_SummonEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "SummonEffect:M1/1" });
            SummonEffectData s = (SummonEffectData)effects[0].Effect;
            Assert.That(s.CardKey, Is.EqualTo("M1"));
            Assert.That(s.Count, Is.EqualTo(1));
        }

        [Test]
        public void Parse_BuffEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "BuffEffect:2/3" });
            BuffEffectData b = (BuffEffectData)effects[0].Effect;
            Assert.That(b.Attack, Is.EqualTo(2));
            Assert.That(b.Health, Is.EqualTo(3));
        }

        [Test]
        public void Parse_BuffEffect_WrongParamCount_Throws()
        {
            Assert.That(() => EffectParser.Parse(new[] { "BuffEffect:2/2/2" }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => EffectParser.Parse(new[] { "BuffEffect:2" }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Parse_SummonEffect_WrongParamCount_Throws()
        {
            Assert.That(() => EffectParser.Parse(new[] { "SummonEffect:M1" }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Parse_MultipleEffects_InOrder()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(
                new[] { "DamageEffect:1", "DrawCardEffect:1" });
            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(effects[0].Effect, Is.TypeOf<DamageEffectData>());
            Assert.That(effects[1].Effect, Is.TypeOf<DrawCardEffectData>());
        }

        [Test]
        public void Parse_GainManaEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "GainManaEffect:2" });
            GainManaEffectData g = (GainManaEffectData)effects[0].Effect;
            Assert.That(g.Amount, Is.EqualTo(2));
        }

        [Test]
        public void Parse_GainArmorEffect()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "GainArmorEffect:4" });
            GainArmorEffectData g = (GainArmorEffectData)effects[0].Effect;
            Assert.That(g.Amount, Is.EqualTo(4));
        }

        [Test]
        public void Parse_DestroyEffect_NoParam()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "DestroyEffect" });
            Assert.That(effects[0].Effect, Is.TypeOf<DestroyEffectData>());
        }

        [Test]
        public void Parse_CompositeEffect_TwoSubEffects()
        {
            IReadOnlyList<TriggeredEffect> effects =
                EffectParser.Parse(new[] { "CompositeEffect:DestroyEffect+SummonEffect:M1/1" });
            CompositeEffectData c = (CompositeEffectData)effects[0].Effect;
            Assert.That(c.Effects.Count, Is.EqualTo(2));
            Assert.That(c.Effects[0], Is.TypeOf<DestroyEffectData>());
            Assert.That(c.Effects[1], Is.TypeOf<SummonEffectData>());
        }

        [Test]
        public void Parse_CompositeEffect_EmptyBody_Throws()
        {
            Assert.That(() => EffectParser.Parse(new[] { "CompositeEffect:" }),
                Throws.InstanceOf<ArgumentException>());
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
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(Array.Empty<string>());
            Assert.That(effects.Count, Is.EqualTo(0));
        }
    }
}
