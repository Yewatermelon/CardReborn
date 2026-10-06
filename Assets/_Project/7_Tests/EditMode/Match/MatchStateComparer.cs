using NUnit.Framework;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T9 辅助：MatchState 逐字段语义比较（序列化往返断言用）。</summary>
    internal static class MatchStateComparer
    {
        public static void AssertEqual(MatchState expected, MatchState actual)
        {
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase), "phase");
            Assert.That(actual.TurnNumber, Is.EqualTo(expected.TurnNumber), "turnNumber");
            Assert.That(actual.ActivePlayerId, Is.EqualTo(expected.ActivePlayerId), "activePlayerId");
            Assert.That(actual.IsFinished, Is.EqualTo(expected.IsFinished), "isFinished");

            foreach (PlayerState ep in expected.Players)
            {
                PlayerState ap = actual.GetPlayer(ep.Id);
                AssertPlayerEqual(ep, ap);
            }
        }

        private static void AssertPlayerEqual(PlayerState expected, PlayerState actual)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id), "player.id");
            Assert.That(actual.FatigueCounter, Is.EqualTo(expected.FatigueCounter), "fatigue");
            AssertHeroEqual(expected.Hero, actual.Hero);
            Assert.That(actual.Mana.Max, Is.EqualTo(expected.Mana.Max), "mana.max");
            Assert.That(actual.Mana.Current, Is.EqualTo(expected.Mana.Current), "mana.current");

            AssertZoneEqual(expected.Deck, actual.Deck);
            AssertZoneEqual(expected.Hand, actual.Hand);
            AssertZoneEqual(expected.Board, actual.Board);
            AssertZoneEqual(expected.Graveyard, actual.Graveyard);
        }

        private static void AssertHeroEqual(HeroState expected, HeroState actual)
        {
            Assert.That(actual.HeroKey, Is.EqualTo(expected.HeroKey), "hero.key");
            Assert.That(actual.HeroPowerKey, Is.EqualTo(expected.HeroPowerKey), "hero.powerKey");
            Assert.That(actual.MaxHealth, Is.EqualTo(expected.MaxHealth), "hero.maxHealth");
            Assert.That(actual.Health, Is.EqualTo(expected.Health), "hero.health");
            Assert.That(actual.Armor, Is.EqualTo(expected.Armor), "hero.armor");
            Assert.That(actual.PowerUsedThisTurn, Is.EqualTo(expected.PowerUsedThisTurn), "hero.powerUsed");
        }

        private static void AssertZoneEqual(Zone expected, Zone actual)
        {
            Assert.That(actual.Type, Is.EqualTo(expected.Type), "zone.type");
            Assert.That(actual.Count, Is.EqualTo(expected.Count), expected.Type + ".count");
            for (int i = 0; i < expected.Count; i++)
            {
                AssertCardEqual(expected.Cards[i], actual.Cards[i], expected.Type + "[" + i + "]");
            }
        }

        private static void AssertCardEqual(CardInstance expected, CardInstance actual, string path)
        {
            Assert.That(actual.InstanceId, Is.EqualTo(expected.InstanceId), path + ".id");
            Assert.That(actual.CardKey, Is.EqualTo(expected.CardKey), path + ".key");
            Assert.That(actual.OwnerId, Is.EqualTo(expected.OwnerId), path + ".owner");
            Assert.That(actual.Attack, Is.EqualTo(expected.Attack), path + ".attack");
            Assert.That(actual.MaxHealth, Is.EqualTo(expected.MaxHealth), path + ".maxHealth");
            Assert.That(actual.Health, Is.EqualTo(expected.Health), path + ".health");
            Assert.That(actual.CurrentZone, Is.EqualTo(expected.CurrentZone), path + ".currentZone");
            Assert.That(actual.Keywords.Flags, Is.EqualTo(expected.Keywords.Flags), path + ".keywords");
            Assert.That(actual.Statuses.Flags, Is.EqualTo(expected.Statuses.Flags), path + ".statuses");
            Assert.That(actual.AttacksUsedThisTurn, Is.EqualTo(expected.AttacksUsedThisTurn), path + ".attacks");
        }
    }
}
