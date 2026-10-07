using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// StateChangeApplier 测试（M4-T10）：验证增量按 Removed→Modified→Added 顺序应用到 MatchState。
    /// 手工构造 MatchState 与 StateChange，不依赖 MatchFactory/MatchController。
    /// </summary>
    [TestFixture]
    public class StateChangeApplierTests
    {
        [Test]
        public void Apply_RootScalarsModified_UpdatesRootFields()
        {
            MatchState state = NewState(activePlayerId: 0, phase: TurnPhase.MatchStart, turnNumber: 1, isFinished: false);
            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Modified, "phase", JsonValue.From("MatchStart"), JsonValue.From("Main")),
                new StateChange(ChangeKind.Modified, "turnNumber", JsonValue.From(1), JsonValue.From(2)),
                new StateChange(ChangeKind.Modified, "activePlayerId", JsonValue.From(0), JsonValue.From(1)),
                new StateChange(ChangeKind.Modified, "isFinished", JsonValue.From(false), JsonValue.From(true))
            };

            StateChangeApplier.Apply(state, changes);

            Assert.That(state.Phase, Is.EqualTo(TurnPhase.Main));
            Assert.That(state.TurnNumber, Is.EqualTo(2));
            Assert.That(state.ActivePlayerId, Is.EqualTo(1));
            Assert.That(state.IsFinished, Is.True);
        }

        [Test]
        public void Apply_HeroAndManaModified_UpdatesPlayerFields()
        {
            MatchState state = NewState(activePlayerId: 0);
            PlayerState player = state.GetPlayer(0);
            player.Hero.Health = 20;
            player.Hero.Armor = 5;
            player.Hero.PowerUsedThisTurn = true;
            player.Mana.Restore(7, 3);

            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Modified, "players[0].hero.health", JsonValue.From(20), JsonValue.From(15)),
                new StateChange(ChangeKind.Modified, "players[0].hero.armor", JsonValue.From(5), JsonValue.From(0)),
                new StateChange(ChangeKind.Modified, "players[0].hero.powerUsedThisTurn", JsonValue.From(true), JsonValue.From(false)),
                new StateChange(ChangeKind.Modified, "players[0].mana.max", JsonValue.From(7), JsonValue.From(8)),
                new StateChange(ChangeKind.Modified, "players[0].mana.current", JsonValue.From(3), JsonValue.From(8))
            };

            StateChangeApplier.Apply(state, changes);

            Assert.That(player.Hero.Health, Is.EqualTo(15));
            Assert.That(player.Hero.Armor, Is.EqualTo(0));
            Assert.That(player.Hero.PowerUsedThisTurn, Is.False);
            Assert.That(player.Mana.Max, Is.EqualTo(8));
            Assert.That(player.Mana.Current, Is.EqualTo(8));
        }

        [Test]
        public void Apply_AddedCard_RebuildsFullCardInstance()
        {
            MatchState state = NewState(activePlayerId: 0);
            PlayerState player = state.GetPlayer(0);

            JsonValue cardJson = JsonValue.Object()
                .Add("instanceId", JsonValue.From(42))
                .Add("cardKey", JsonValue.From("CARD_TEST_MINION"))
                .Add("ownerId", JsonValue.From(0))
                .Add("attack", JsonValue.From(3))
                .Add("maxHealth", JsonValue.From(4))
                .Add("health", JsonValue.From(2))
                .Add("keywords", JsonValue.From((int)(Keyword.Taunt | Keyword.Charge)))
                .Add("statuses", JsonValue.From((int)(StatusFlags.Frozen | StatusFlags.DivineShield)))
                .Add("attacksUsedThisTurn", JsonValue.From(1));

            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Added, "players[0].hand", JsonValue.Null(), cardJson)
            };

            StateChangeApplier.Apply(state, changes);

            Assert.That(player.Hand.Count, Is.EqualTo(1));
            CardInstance card = player.Hand.Cards[0];
            Assert.That(card.InstanceId, Is.EqualTo(42));
            Assert.That(card.CardKey, Is.EqualTo("CARD_TEST_MINION"));
            Assert.That(card.OwnerId, Is.EqualTo(0));
            Assert.That(card.Attack, Is.EqualTo(3));
            Assert.That(card.MaxHealth, Is.EqualTo(4));
            Assert.That(card.Health, Is.EqualTo(2));
            Assert.That(card.Keywords.Flags, Is.EqualTo(Keyword.Taunt | Keyword.Charge));
            Assert.That(card.Statuses.Flags, Is.EqualTo(StatusFlags.Frozen | StatusFlags.DivineShield));
            Assert.That(card.AttacksUsedThisTurn, Is.EqualTo(1));
            Assert.That(card.CurrentZone, Is.EqualTo(ZoneType.Hand));
        }

        [Test]
        public void Apply_RemovedCard_RemovesFromZone()
        {
            MatchState state = NewState(activePlayerId: 0);
            PlayerState player = state.GetPlayer(0);
            CardInstance card = CardInstance.Restore(10, "CARD_TEST", 0, 2, 3, 3, Keyword.None, StatusFlags.None, 0);
            player.Hand.Add(card);

            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Removed, "players[0].hand", JsonValue.From(10), JsonValue.Null())
            };

            StateChangeApplier.Apply(state, changes);

            Assert.That(player.Hand.Count, Is.EqualTo(0));
            Assert.That(card.CurrentZone, Is.Null);
        }

        [Test]
        public void Apply_CrossZoneMove_RemovedThenAdded_MovesCard()
        {
            MatchState state = NewState(activePlayerId: 0);
            PlayerState player = state.GetPlayer(0);
            CardInstance card = CardInstance.Restore(7, "CARD_TEST", 0, 2, 3, 3, Keyword.None, StatusFlags.None, 0);
            player.Hand.Add(card);

            JsonValue cardJson = JsonValue.Object()
                .Add("instanceId", JsonValue.From(7))
                .Add("cardKey", JsonValue.From("CARD_TEST"))
                .Add("ownerId", JsonValue.From(0))
                .Add("attack", JsonValue.From(2))
                .Add("maxHealth", JsonValue.From(3))
                .Add("health", JsonValue.From(3))
                .Add("keywords", JsonValue.From(0))
                .Add("statuses", JsonValue.From(0))
                .Add("attacksUsedThisTurn", JsonValue.From(0));

            // 乱序：Added 先、Removed 后，验证 Applier 内部按 Removed→Modified→Added 排序
            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Added, "players[0].board", JsonValue.Null(), cardJson),
                new StateChange(ChangeKind.Removed, "players[0].hand", JsonValue.From(7), JsonValue.Null())
            };

            StateChangeApplier.Apply(state, changes);

            Assert.That(player.Hand.Count, Is.EqualTo(0));
            Assert.That(player.Board.Count, Is.EqualTo(1));
            Assert.That(player.Board.Cards[0].InstanceId, Is.EqualTo(7));
            Assert.That(player.Board.Cards[0].CurrentZone, Is.EqualTo(ZoneType.Board));
        }

        [Test]
        public void Apply_ModifiedCardFields_UpdatesExistingCard()
        {
            MatchState state = NewState(activePlayerId: 0);
            PlayerState player = state.GetPlayer(0);
            CardInstance card = CardInstance.Restore(5, "CARD_TEST", 0, 2, 3, 3, Keyword.None, StatusFlags.None, 0);
            player.Board.Add(card);

            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Modified, "players[0].board[0].attack", JsonValue.From(2), JsonValue.From(5)),
                new StateChange(ChangeKind.Modified, "players[0].board[0].health", JsonValue.From(3), JsonValue.From(1)),
                new StateChange(ChangeKind.Modified, "players[0].board[0].keywords", JsonValue.From(0), JsonValue.From((int)Keyword.Taunt)),
                new StateChange(ChangeKind.Modified, "players[0].board[0].statuses", JsonValue.From(0), JsonValue.From((int)StatusFlags.Frozen)),
                new StateChange(ChangeKind.Modified, "players[0].board[0].attacksUsedThisTurn", JsonValue.From(0), JsonValue.From(2))
            };

            StateChangeApplier.Apply(state, changes);

            Assert.That(card.Attack, Is.EqualTo(5));
            Assert.That(card.Health, Is.EqualTo(1));
            Assert.That(card.Keywords.Flags, Is.EqualTo(Keyword.Taunt));
            Assert.That(card.Statuses.Flags, Is.EqualTo(StatusFlags.Frozen));
            Assert.That(card.AttacksUsedThisTurn, Is.EqualTo(2));
        }

        [Test]
        public void Apply_UnknownPath_ThrowsFormatException()
        {
            MatchState state = NewState(activePlayerId: 0);
            List<StateChange> changes = new List<StateChange>
            {
                new StateChange(ChangeKind.Modified, "players[0].unknownField", JsonValue.From(1), JsonValue.From(2))
            };

            Assert.Throws<FormatException>(() => StateChangeApplier.Apply(state, changes));
        }

        private static MatchState NewState(int activePlayerId, TurnPhase phase = TurnPhase.MatchStart, int turnNumber = 1, bool isFinished = false)
        {
            HeroState hero0 = new HeroState("HERO_MAGE", "POWER_MAGE", 30);
            HeroState hero1 = new HeroState("HERO_WARRIOR", "POWER_WARRIOR", 30);
            PlayerState p0 = new PlayerState(0, hero0, new ManaPool(0, 0));
            PlayerState p1 = new PlayerState(1, hero1, new ManaPool(0, 0));
            MatchState state = new MatchState(p0, p1, activePlayerId)
            {
                Phase = phase,
                TurnNumber = turnNumber,
                IsFinished = isFinished
            };
            return state;
        }
    }
}
