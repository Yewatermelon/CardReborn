using System;
using System.Collections.Generic;
using Card.Domain.Match.Effects;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T5 AC-1/2/3：EffectParser 解析 Trigger 前缀。</summary>
    [TestFixture]
    public sealed class TriggerParserTests
    {
        [Test]
        public void Parse_WithOnPlayPrefix()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "OnPlay:DamageEffect:3" });
            Assert.That(effects.Count, Is.EqualTo(1));
            Assert.That(effects[0].Trigger, Is.EqualTo(Trigger.OnPlay));
            Assert.That(effects[0].Effect, Is.TypeOf<DamageEffectData>());
            Assert.That(((DamageEffectData)effects[0].Effect).Amount, Is.EqualTo(3));
        }

        [Test]
        public void Parse_WithOnDeathPrefix()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "OnDeath:DamageEffect:2" });
            Assert.That(effects[0].Trigger, Is.EqualTo(Trigger.OnDeath));
        }

        [Test]
        public void Parse_NoPrefix_DefaultsToOnPlay()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "HealEffect:5" });
            Assert.That(effects[0].Trigger, Is.EqualTo(Trigger.OnPlay));
            Assert.That(effects[0].Effect, Is.TypeOf<HealEffectData>());
        }

        [Test]
        public void Parse_OnTurnStart_And_OnTurnEnd()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(
                new[] { "OnTurnStart:DrawCardEffect:1", "OnTurnEnd:DamageEffect:1" });
            Assert.That(effects[0].Trigger, Is.EqualTo(Trigger.OnTurnStart));
            Assert.That(effects[1].Trigger, Is.EqualTo(Trigger.OnTurnEnd));
        }

        [Test]
        public void Parse_OnSummonPrefix()
        {
            IReadOnlyList<TriggeredEffect> effects = EffectParser.Parse(new[] { "OnSummon:DamageEffect:1" });
            Assert.That(effects[0].Trigger, Is.EqualTo(Trigger.OnSummon));
        }

        [Test]
        public void Parse_UnknownTrigger_Throws()
        {
            Assert.That(() => EffectParser.Parse(new[] { "OnFoo:DamageEffect:1" }),
                Throws.InstanceOf<ArgumentException>());
        }
    }
}
