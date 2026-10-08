using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Tests.EditMode.Match;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class MatchEventPumpTests
    {
        private GameObject _root = null!;
        private CardView _prefab = null!;
        private HandView _handView = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Pump_Test");
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _prefab.gameObject.SetActive(false);
            _handView = _root.AddComponent<HandView>();
            _handView._cardPrefab = _prefab;
            _handView._spacing = 100f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        private static EventLog NewLogWithHistory()
        {
            var log = new EventLog();
            log.Emit(new TurnStartedEvent(1, 0));
            log.Emit(new CardDrawnEvent(0, 42));
            return log;
        }

        [Test]
        public void Constructor_SkipsExistingHistory()
        {
            EventLog log = NewLogWithHistory();
            var pump = new MatchEventPump(log.Events);

            Assert.That(pump.PendingCount, Is.EqualTo(0));
            Assert.That(pump.Pump(), Is.EqualTo(0));
        }

        [Test]
        public void Pump_DispatchesNewEventsInOrder()
        {
            EventLog log = NewLogWithHistory();
            var pump = new MatchEventPump(log.Events);
            var received = new List<GameEvent>();
            pump.EventAppended += e => received.Add(e);

            log.Emit(new PhaseChangedEvent(TurnPhase.Main, TurnPhase.TurnEnd));
            log.Emit(new TurnEndedEvent(1, 0));
            log.Emit(new TurnStartedEvent(2, 1));

            Assert.That(pump.PendingCount, Is.EqualTo(3));
            Assert.That(pump.Pump(), Is.EqualTo(3));
            Assert.That(received.Select(e => e.GetType()), Is.EqualTo(new[]
            {
                typeof(PhaseChangedEvent), typeof(TurnEndedEvent), typeof(TurnStartedEvent)
            }));
            Assert.That(pump.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Pump_NoNewEvents_ReturnsZero()
        {
            EventLog log = NewLogWithHistory();
            var pump = new MatchEventPump(log.Events);
            int calls = 0;
            pump.EventAppended += _ => calls++;

            Assert.That(pump.Pump(), Is.EqualTo(0));
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void EndToEnd_EndTurnDraw_RefreshesHandView()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState match = MatchControllerFixtures.NewMatch(db, deck, seed: 7);
            var controller = new MatchController(match, db);
            var pump = new MatchEventPump(controller.Events);

            RefreshHandFromActivePlayer(match, db, _handView);
            int before = _handView.ChildCount;

            int seat = match.ActivePlayerId;
            CommandResult result = controller.Submit(new EndTurnCommand(seat));
            Assert.That(result.IsValid, Is.True, "EndTurn 应被接受：" + result.Error);

            pump.EventAppended += _ => RefreshHandFromActivePlayer(match, db, _handView);
            int dispatched = pump.Pump();

            PlayerState nowActive = match.GetPlayer(match.ActivePlayerId);
            Assert.That(dispatched, Is.GreaterThan(0));
            Assert.That(nowActive.Id, Is.Not.EqualTo(seat), "EndTurn 后应轮到对方");
            Assert.That(_handView.ChildCount, Is.EqualTo(nowActive.Hand.Count));
            Assert.That(_handView.ChildCount, Is.GreaterThan(before), "新行动方抽了一张牌，手牌应变多");
        }

        private static void RefreshHandFromActivePlayer(MatchState match, CardDatabase db, HandView view)
        {
            PlayerState active = match.GetPlayer(match.ActivePlayerId);
            List<ICardViewData> data = active.Hand.Cards
                .Select(c => (ICardViewData)CardViewData.FromInstance(
                    db.RequireCard(c.CardKey), c, KeyPassthroughTextResolver.Instance))
                .ToList();
            view.SetCards(data);
        }
    }
}
