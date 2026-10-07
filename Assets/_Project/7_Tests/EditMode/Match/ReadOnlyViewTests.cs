using System.Collections.Generic;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M5-T8：只读视图模型（kernel 侧）。验证 IReadOnly* 接口是活视图（零拷贝实时映射）、
    /// 逐字段可读、序列化往返后两份只读视图深等（PVP 下发状态可直接渲染的证明）。
    /// </summary>
    [TestFixture]
    public sealed class ReadOnlyViewTests
    {
        private static MatchState NewMatch(out CardDatabase db, int seed = 42)
        {
            db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            return MatchControllerFixtures.NewMatch(db, deck, seed);
        }

        [Test]
        public void View_LiveReflectsUnderlyingMutation()
        {
            MatchState state = NewMatch(out _);
            IReadOnlyMatchState view = state;
            IReadOnlyHeroState heroView = view.ActivePlayer.Hero;
            int before = heroView.Health;

            state.ActivePlayer.Hero.TakeDamage(3);

            Assert.That(heroView.Health, Is.EqualTo(before - 3), "只读视图必须实时映射底层状态（零拷贝）。");
        }

        [Test]
        public void View_ExposesAllReadableFields()
        {
            MatchState state = NewMatch(out _);
            IReadOnlyMatchState view = state;

            Assert.That(view.TurnNumber, Is.EqualTo(state.TurnNumber));
            Assert.That(view.Phase, Is.EqualTo(state.Phase));
            Assert.That(view.ActivePlayerId, Is.EqualTo(state.ActivePlayerId));
            Assert.That(view.IsFinished, Is.EqualTo(state.IsFinished));
            Assert.That(view.Players.Count, Is.EqualTo(2));

            IReadOnlyPlayerState seat = view.GetPlayer(view.ActivePlayerId);
            Assert.That(seat.Id, Is.EqualTo(view.ActivePlayerId));
            Assert.That(seat.Hero.HeroKey, Is.Not.Null.And.Not.Empty);
            Assert.That(seat.Hero.MaxHealth, Is.GreaterThan(0));
            Assert.That(seat.Mana.Max, Is.GreaterThan(0));
            Assert.That(seat.Deck.Count, Is.GreaterThan(0));
            Assert.That(seat.Hand.Count, Is.GreaterThan(0));
            Assert.That(seat.Board.Type, Is.EqualTo(ZoneType.Board));

            IReadOnlyCardInstance card = seat.Hand.Cards[0];
            Assert.That(card.CardKey, Is.Not.Null.And.Not.Empty);
            Assert.That(card.OwnerId, Is.EqualTo(seat.Id));
            Assert.That(card.CurrentZone, Is.EqualTo(ZoneType.Hand));
        }

        [Test]
        public void View_CardFlags_AreValueTypeCopies()
        {
            MatchState state = NewMatch(out _);
            IReadOnlyCardInstance card = state.GetPlayer(state.ActivePlayerId).Hand.Cards[0];

            Keyword keywords = card.KeywordFlags;
            StatusFlags statuses = card.StatusFlags;

            Assert.That(keywords, Is.EqualTo(card.KeywordFlags));
            Assert.That(statuses, Is.EqualTo(card.StatusFlags));
        }

        [Test]
        public void View_UnknownSeat_Throws()
        {
            MatchState state = NewMatch(out _);
            IReadOnlyMatchState view = state;

            Assert.That(() => view.GetPlayer(99), Throws.ArgumentException);
        }

        [Test]
        public void MatchController_View_IsReadOnlyFacetOfSameState()
        {
            MatchState state = NewMatch(out CardDatabase db);
            var controller = new MatchController(state, db);

            IReadOnlyMatchState view = controller.View;

            Assert.That(view.TurnNumber, Is.EqualTo(state.TurnNumber));
            state.TurnNumber = 9;
            Assert.That(view.TurnNumber, Is.EqualTo(9), "View 必须与权威状态同源。");
        }

        [Test]
        public void View_AfterSerializeRoundTrip_DeepEqualsOriginal()
        {
            MatchState original = NewMatch(out _);
            string json = MatchStateSerializer.Serialize(original);
            MatchState clone = MatchStateSerializer.Deserialize(json);

            IReadOnlyMatchState a = original;
            IReadOnlyMatchState b = clone;

            Assert.That(b.TurnNumber, Is.EqualTo(a.TurnNumber));
            Assert.That(b.Phase, Is.EqualTo(a.Phase));
            Assert.That(b.ActivePlayerId, Is.EqualTo(a.ActivePlayerId));
            Assert.That(b.IsFinished, Is.EqualTo(a.IsFinished));

            foreach (IReadOnlyPlayerState pa in a.Players)
            {
                IReadOnlyPlayerState pb = b.GetPlayer(pa.Id);
                Assert.That(pb.Hero.HeroKey, Is.EqualTo(pa.Hero.HeroKey));
                Assert.That(pb.Hero.Health, Is.EqualTo(pa.Hero.Health));
                Assert.That(pb.Hero.Armor, Is.EqualTo(pa.Hero.Armor));
                Assert.That(pb.Mana.Max, Is.EqualTo(pa.Mana.Max));
                Assert.That(pb.Mana.Current, Is.EqualTo(pa.Mana.Current));
                Assert.That(pb.FatigueCounter, Is.EqualTo(pa.FatigueCounter));
                AssertZoneDeepEqual(pa.Hand, pb.Hand);
                AssertZoneDeepEqual(pa.Deck, pb.Deck);
                AssertZoneDeepEqual(pa.Board, pb.Board);
                AssertZoneDeepEqual(pa.Graveyard, pb.Graveyard);
            }
        }

        private static void AssertZoneDeepEqual(IReadOnlyZone a, IReadOnlyZone b)
        {
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                IReadOnlyCardInstance ca = a.Cards[i];
                IReadOnlyCardInstance cb = b.Cards[i];
                Assert.That(cb.InstanceId, Is.EqualTo(ca.InstanceId));
                Assert.That(cb.CardKey, Is.EqualTo(ca.CardKey));
                Assert.That(cb.OwnerId, Is.EqualTo(ca.OwnerId));
                Assert.That(cb.Attack, Is.EqualTo(ca.Attack));
                Assert.That(cb.MaxHealth, Is.EqualTo(ca.MaxHealth));
                Assert.That(cb.Health, Is.EqualTo(ca.Health));
                Assert.That(cb.KeywordFlags, Is.EqualTo(ca.KeywordFlags));
                Assert.That(cb.StatusFlags, Is.EqualTo(ca.StatusFlags));
            }
        }

        [Test]
        public void CommandAuthority_Submit_RoutesThroughMatchController()
        {
            MatchState state = NewMatch(out CardDatabase db);
            ICommandAuthority authority = new MatchController(state, db);
            int activeSeat = state.ActivePlayerId;
            int turnBefore = state.TurnNumber;

            CommandResult result = authority.Submit(new EndTurnCommand(activeSeat));

            Assert.That(result.IsValid, Is.True, "submit failed: " + result.Error);
            Assert.That(state.TurnNumber, Is.EqualTo(turnBefore + 1));
        }
    }
}
