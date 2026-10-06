using System;
using System.Linq;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T1：英雄/玩家/对局状态骨架的构造语义。</summary>
    public sealed class MatchStateModelTests
    {
        private static HeroState CreateHero(string key = "MAGE")
        {
            return new HeroState(key, "MAGE_FIREBLAST", maxHealth: 30);
        }

        [Test]
        public void HeroState_Ctor_HealthDefaultsToMax()
        {
            HeroState hero = CreateHero();

            Assert.That(hero.MaxHealth, Is.EqualTo(30));
            Assert.That(hero.Health, Is.EqualTo(30));
            Assert.That(hero.Armor, Is.EqualTo(0));
            Assert.That(hero.PowerUsedThisTurn, Is.False);
        }

        [Test]
        public void HeroState_Ctor_WithExplicitHealth()
        {
            HeroState hero = new HeroState("MAGE", "MAGE_FIREBLAST", maxHealth: 30, health: 18);

            Assert.That(hero.Health, Is.EqualTo(18));
        }

        [Test]
        public void HeroState_Ctor_WhenArgumentsInvalid_Throws()
        {
            Assert.Throws<ArgumentException>(() => new HeroState(" ", "POWER", 30));
            Assert.Throws<ArgumentException>(() => new HeroState("MAGE", " ", 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeroState("MAGE", "POWER", maxHealth: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeroState("MAGE", "POWER", 30, health: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeroState("MAGE", "POWER", 30, health: 31));
        }

        [Test]
        public void PlayerState_Ctor_ExposesIdHeroAndMana()
        {
            HeroState hero = CreateHero();
            ManaPool mana = new ManaPool(max: 2, current: 2);

            PlayerState player = new PlayerState(id: 1, hero, mana);

            Assert.That(player.Id, Is.EqualTo(1));
            Assert.AreSame(hero, player.Hero);
            Assert.AreSame(mana, player.Mana);
        }

        [Test]
        public void PlayerState_Ctor_WhenArgumentNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerState(0, null!, new ManaPool()));
            Assert.Throws<ArgumentNullException>(() => new PlayerState(0, CreateHero(), null!));
        }

        [Test]
        public void MatchState_Ctor_IsPlayableWithTwoPlayers()
        {
            PlayerState first = new PlayerState(0, CreateHero("MAGE"), new ManaPool());
            PlayerState second = new PlayerState(1, CreateHero("PRIEST"), new ManaPool());

            MatchState state = new MatchState(first, second, activePlayerId: 0);

            Assert.That(state.Phase, Is.EqualTo(TurnPhase.MatchStart));
            Assert.That(state.TurnNumber, Is.EqualTo(1));
            Assert.AreSame(first, state.GetPlayer(0));
            Assert.AreSame(second, state.GetPlayer(1));
            Assert.That(state.Players.Count, Is.EqualTo(2));
        }

        [Test]
        public void MatchState_ActivePlayer_TracksActivePlayerId()
        {
            PlayerState first = new PlayerState(0, CreateHero(), new ManaPool());
            PlayerState second = new PlayerState(1, CreateHero(), new ManaPool());
            MatchState state = new MatchState(first, second, activePlayerId: 0);

            Assert.AreSame(first, state.ActivePlayer);

            state.ActivePlayerId = 1;

            Assert.AreSame(second, state.ActivePlayer);
        }

        [Test]
        public void MatchState_GetPlayer_WhenIdUnknown_Throws()
        {
            PlayerState first = new PlayerState(0, CreateHero(), new ManaPool());
            PlayerState second = new PlayerState(1, CreateHero(), new ManaPool());
            MatchState state = new MatchState(first, second, activePlayerId: 0);

            Assert.Throws<ArgumentException>(() => state.GetPlayer(2));
        }

        [Test]
        public void MatchState_Ctor_WhenPlayerIdsDuplicate_Throws()
        {
            PlayerState first = new PlayerState(0, CreateHero(), new ManaPool());
            PlayerState second = new PlayerState(0, CreateHero(), new ManaPool());

            Assert.Throws<ArgumentException>(() => new MatchState(first, second, activePlayerId: 0));
        }

        [Test]
        public void MatchState_Ctor_WhenActivePlayerMissing_Throws()
        {
            PlayerState first = new PlayerState(0, CreateHero(), new ManaPool());
            PlayerState second = new PlayerState(1, CreateHero(), new ManaPool());

            Assert.Throws<ArgumentException>(() => new MatchState(first, second, activePlayerId: 2));
        }

        [Test]
        public void MatchState_Ctor_WhenArgumentNull_Throws()
        {
            PlayerState player = new PlayerState(0, CreateHero(), new ManaPool());

            Assert.Throws<ArgumentNullException>(() => new MatchState(null!, player, 0));
            Assert.Throws<ArgumentNullException>(() => new MatchState(player, null!, 0));
        }

        [Test]
        public void MatchState_Players_ExposesBothSeats()
        {
            PlayerState first = new PlayerState(0, CreateHero(), new ManaPool());
            PlayerState second = new PlayerState(1, CreateHero(), new ManaPool());
            MatchState state = new MatchState(first, second, activePlayerId: 0);

            var ids = state.Players.Select(p => p.Id).OrderBy(id => id).ToArray();

            Assert.That(ids, Is.EqualTo(new[] { 0, 1 }));
        }
    }
}
