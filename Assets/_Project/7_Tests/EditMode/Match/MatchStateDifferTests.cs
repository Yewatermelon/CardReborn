using System;
using System.Collections.Generic;
using NUnit.Framework;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M3-T10 失败测试（红）：StateChange/MatchStateDiffer 尚未实现，应先编译失败。</summary>
    [TestFixture]
    public sealed class MatchStateDifferTests
    {
        private static CardInstance NewCard(string key, int instanceId, int ownerId, Keyword keywords = Keyword.None)
        {
            return CardInstance.FromDefinition(MatchTestCards.Minion(key, keywords: keywords), instanceId, ownerId);
        }

        /// <summary>每次调用生成全新独立状态（不与其它状态共享对象）。</summary>
        private static MatchState BuildBaseState()
        {
            PlayerState p0 = new PlayerState(0, new HeroState("HERO_A", "POWER_A", 30), new ManaPool(max: 3, current: 2));
            PlayerState p1 = new PlayerState(1, new HeroState("HERO_B", "POWER_B", 30), new ManaPool(max: 3, current: 2));

            p0.Deck.Add(NewCard("CARD_D0", 0, 0));
            p0.Hand.Add(NewCard("CARD_H0", 1, 0));
            p0.Board.Add(NewCard("CARD_B0", 2, 0, Keyword.Taunt));
            p1.Board.Add(NewCard("CARD_B1", 10, 1));

            return new MatchState(p0, p1, activePlayerId: 0)
            {
                Phase = TurnPhase.Main,
                TurnNumber = 5
            };
        }

        private static IReadOnlyList<StateChange> Diff(MatchState before, MatchState after)
        {
            return MatchStateDiffer.Diff(before, after);
        }

        [Test]
        public void Diff_IdenticalStates_ReturnsEmpty()
        {
            IReadOnlyList<StateChange> changes = Diff(BuildBaseState(), BuildBaseState());

            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void Diff_TurnNumberChanged_ReturnsSingleModified()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.TurnNumber = 6;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Kind, Is.EqualTo(ChangeKind.Modified));
            Assert.That(changes[0].Path, Is.EqualTo("turnNumber"));
            Assert.That(changes[0].OldValue.IntValue, Is.EqualTo(5));
            Assert.That(changes[0].NewValue.IntValue, Is.EqualTo(6));
        }

        [Test]
        public void Diff_PhaseChanged_UsesEnumInteger()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.Phase = TurnPhase.TurnEnd;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Path, Is.EqualTo("phase"));
            Assert.That(changes[0].OldValue.IntValue, Is.EqualTo((int)TurnPhase.Main));
            Assert.That(changes[0].NewValue.IntValue, Is.EqualTo((int)TurnPhase.TurnEnd));
        }

        [Test]
        public void Diff_ActivePlayerChanged_ReturnsModified()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.ActivePlayerId = 1;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Path, Is.EqualTo("activePlayerId"));
            Assert.That(changes[0].NewValue.IntValue, Is.EqualTo(1));
        }

        [Test]
        public void Diff_IsFinishedChanged_UsesBooleans()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.IsFinished = true;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Path, Is.EqualTo("isFinished"));
            Assert.That(changes[0].OldValue.BoolValue, Is.False);
            Assert.That(changes[0].NewValue.BoolValue, Is.True);
        }

        [Test]
        public void Diff_FatigueChanged_ReturnsPlayerPath()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.GetPlayer(0).FatigueCounter = 2;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Path, Is.EqualTo("players[0].fatigueCounter"));
            Assert.That(changes[0].OldValue.IntValue, Is.EqualTo(0));
            Assert.That(changes[0].NewValue.IntValue, Is.EqualTo(2));
        }

        [Test]
        public void Diff_HeroFieldsChanged_ReturnsThreeHeroPaths()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.GetPlayer(0).Hero.Health = 25;
            after.GetPlayer(0).Hero.Armor = 5;
            after.GetPlayer(0).Hero.PowerUsedThisTurn = true;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].Path, Is.EqualTo("players[0].hero.health"));
            Assert.That(changes[0].NewValue.IntValue, Is.EqualTo(25));
            Assert.That(changes[1].Path, Is.EqualTo("players[0].hero.armor"));
            Assert.That(changes[1].NewValue.IntValue, Is.EqualTo(5));
            Assert.That(changes[2].Path, Is.EqualTo("players[0].hero.powerUsedThisTurn"));
            Assert.That(changes[2].NewValue.BoolValue, Is.True);
        }

        [Test]
        public void Diff_ManaChanged_ReturnsManaPaths()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.GetPlayer(1).Mana.BeginTurn(10); // max 3→4，current 回满 4

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes[0].Path, Is.EqualTo("players[1].mana.max"));
            Assert.That(changes[0].NewValue.IntValue, Is.EqualTo(4));
            Assert.That(changes[1].Path, Is.EqualTo("players[1].mana.current"));
            Assert.That(changes[1].NewValue.IntValue, Is.EqualTo(4));
        }

        [Test]
        public void Diff_ZoneMembershipChanged_ReturnsAddedAndRemoved()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.GetPlayer(0).Deck.Remove(after.GetPlayer(0).Deck.Cards[0]); // 移除实例 0
            after.GetPlayer(0).Hand.Add(NewCard("CARD_H1", 3, 0));            // 新增实例 3

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(2));

            StateChange removed = changes[0];
            Assert.That(removed.Kind, Is.EqualTo(ChangeKind.Removed));
            Assert.That(removed.Path, Is.EqualTo("players[0].deck"));
            Assert.That(removed.OldValue.IntValue, Is.EqualTo(0));
            Assert.That(removed.NewValue.Kind, Is.EqualTo(JsonKind.Null));

            StateChange added = changes[1];
            Assert.That(added.Kind, Is.EqualTo(ChangeKind.Added));
            Assert.That(added.Path, Is.EqualTo("players[0].hand"));
            Assert.That(added.OldValue.Kind, Is.EqualTo(JsonKind.Null));
            Assert.That(added.NewValue.IntValue, Is.EqualTo(3));
        }

        [Test]
        public void Diff_BoardCardFieldsChanged_ReturnsPerFieldModified()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            CardInstance card = after.GetPlayer(0).Board.Cards[0];
            card.Health = 1;
            card.AttacksUsedThisTurn = 1;
            card.Keywords.Add(Keyword.Charge);
            card.Statuses.Add(StatusFlags.Frozen);

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(4));
            Assert.That(changes[0].Path, Is.EqualTo("players[0].board[0].health"), "attack 未变不应出现，首条应为 health");

            IReadOnlyList<string> paths = new List<string>
            {
                changes[0].Path, changes[1].Path, changes[2].Path, changes[3].Path
            };
            Assert.That(paths, Does.Contain("players[0].board[0].attacksUsedThisTurn"));
            Assert.That(paths, Does.Contain("players[0].board[0].keywords"));
            Assert.That(paths, Does.Contain("players[0].board[0].statuses"));
        }

        [Test]
        public void Diff_MultipleChanges_AreListedInDeterministicOrder()
        {
            MatchState before = BuildBaseState();
            MatchState after = BuildBaseState();
            after.TurnNumber = 6;
            after.GetPlayer(1).Hero.Health = 10;
            after.GetPlayer(0).Hero.Health = 20;

            IReadOnlyList<StateChange> changes = Diff(before, after);

            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].Path, Is.EqualTo("turnNumber"));
            Assert.That(changes[1].Path, Is.EqualTo("players[0].hero.health"));
            Assert.That(changes[2].Path, Is.EqualTo("players[1].hero.health"));
        }

        [Test]
        public void Diff_NullArgument_Throws()
        {
            MatchState state = BuildBaseState();

            Assert.Throws<ArgumentNullException>(() => MatchStateDiffer.Diff(null!, state));
            Assert.Throws<ArgumentNullException>(() => MatchStateDiffer.Diff(state, null!));
        }
    }
}
