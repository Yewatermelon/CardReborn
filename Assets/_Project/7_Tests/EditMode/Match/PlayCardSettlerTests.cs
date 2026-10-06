using System.Linq;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T4 AC-8：出牌端到端——法力、进场、战吼效果、事件。</summary>
    [TestFixture]
    public sealed class PlayCardSettlerTests
    {
        private static readonly HeroDefinition[] Heroes =
        {
            new HeroDefinition { Id = 1, Key = "HERO_A", Health = 30, HeroPowerKey = "POWER_A" },
            new HeroDefinition { Id = 2, Key = "HERO_B", Health = 30, HeroPowerKey = "POWER_B" }
        };

        private static readonly HeroPowerDefinition[] HeroPowers =
        {
            new HeroPowerDefinition { Id = 1, Key = "POWER_A", Cost = 2, TargetRule = TargetRule.Any },
            new HeroPowerDefinition { Id = 2, Key = "POWER_B", Cost = 2, TargetRule = TargetRule.None }
        };

        private static CardDefinition MinionWithEffect(string key, int cost, params string[] effects)
        {
            return new CardDefinition
            {
                Id = 7000 + cost,
                Key = key,
                Cost = cost,
                Type = CardType.Minion,
                Attack = 2,
                Health = 2,
                TargetRule = effects.Any(e => e.StartsWith("Damage") || e.StartsWith("Buff"))
                    ? TargetRule.Any
                    : TargetRule.None,
                Effects = effects
            };
        }

        private static CardDefinition SpellWithEffect(string key, int cost, params string[] effects)
        {
            return new CardDefinition
            {
                Id = 8000 + cost,
                Key = key,
                Cost = cost,
                Type = CardType.Spell,
                TargetRule = effects.Any(e => e.StartsWith("Damage") || e.StartsWith("Buff"))
                    ? TargetRule.Any
                    : TargetRule.None,
                Effects = effects
            };
        }

        private static CardDatabase BuildDb(params CardDefinition[] cards)
        {
            return new CardDatabase(new ConfigBundle(
                cards: cards,
                heroes: Heroes,
                heroPowers: HeroPowers,
                rarityWeights: new[] { new RarityWeight { Rarity = CardRarity.Common, Weight = 100 } },
                gacha: new GachaConfig(),
                rules: new RulesConfig()));
        }

        [Test]
        public void PlayMinion_SpendsMana_EntersBoard_EmitsCardPlayed()
        {
            CardDatabase db = BuildDb(MinionWithEffect("M_BASIC", 2));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            CardInstance card = RuleEngineTestHelpers.AddToHand(self, db.RequireCard("M_BASIC"), 100);
            MatchController controller = new MatchController(state, db);

            CommandResult result = controller.Submit(
                new PlayCardCommand(0, 100, TargetRef.None));

            Assert.That(result.IsValid, Is.True);
            Assert.That(self.Mana.Current, Is.EqualTo(8));
            Assert.That(self.Hand.Contains(card), Is.False);
            Assert.That(self.Board.Contains(card), Is.True);
            Assert.That(controller.Events.OfType<CardPlayedEvent>().Single().CardInstanceId,
                Is.EqualTo(100));
        }

        [Test]
        public void PlaySpell_SpendsMana_GoesToGraveyard_ResolvesEffect()
        {
            CardDatabase db = BuildDb(SpellWithEffect("S_DAMAGE", 2, "DamageEffect:4"));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            CardInstance card = RuleEngineTestHelpers.AddToHand(self, db.RequireCard("S_DAMAGE"), 100);
            MatchController controller = new MatchController(state, db);

            CommandResult result = controller.Submit(
                new PlayCardCommand(0, 100, TargetRef.ForHero(1)));

            Assert.That(result.IsValid, Is.True);
            Assert.That(self.Mana.Current, Is.EqualTo(8));
            Assert.That(self.Hand.Contains(card), Is.False);
            Assert.That(self.Graveyard.Contains(card), Is.True);
            Assert.That(enemy.Hero.Health, Is.EqualTo(26));
            Assert.That(controller.Events.OfType<DamageEvent>().Single().Amount, Is.EqualTo(4));
        }

        [Test]
        public void PlayMinion_WithBattlecryDamage_AppliesToTarget()
        {
            CardDatabase db = BuildDb(MinionWithEffect("M_BATTLECRY", 2, "DamageEffect:2"));
            MatchState state = RuleEngineTestHelpers.BuildState(activePlayerId: 0);
            PlayerState self = state.GetPlayer(0);
            PlayerState enemy = state.GetPlayer(1);
            RuleEngineTestHelpers.AddToHand(self, db.RequireCard("M_BATTLECRY"), 100);
            MatchController controller = new MatchController(state, db);

            controller.Submit(new PlayCardCommand(0, 100, TargetRef.ForHero(1)));

            Assert.That(self.Board.Count, Is.EqualTo(1));
            Assert.That(enemy.Hero.Health, Is.EqualTo(28));
        }
    }
}
