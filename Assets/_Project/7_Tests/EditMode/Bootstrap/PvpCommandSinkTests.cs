using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Bootstrap.Battle.Net;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Tests.EditMode.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// 联机命令出口测试（M14-T3 AC-8）：seq 自 1 自增、信封 JSON 结构、
    /// 在途登记经 PendingActionFeedback 生效、卡实例 Id 提取（PlayCard/Attack）。
    /// </summary>
    [TestFixture]
    public class PvpCommandSinkTests
    {
        private FakeNetLink _link = null!;
        private NetMatchDriver _driver = null!;
        private PendingCommandTracker _tracker = null!;
        private PendingActionFeedback _feedback = null!;
        private PvpCommandSink _sink = null!;
        private readonly List<UnityEngine.Object> _trash = new List<UnityEngine.Object>();
        private readonly Dictionary<int, CardView> _cards = new Dictionary<int, CardView>();

        [SetUp]
        public void SetUp()
        {
            _link = new FakeNetLink();
            _driver = new NetMatchDriver(_link, 0);
            _tracker = new PendingCommandTracker();
            _feedback = new PendingActionFeedback(_tracker, Locate, null, null);
            _sink = new PvpCommandSink(_driver, _feedback);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object asset in _trash)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _trash.Clear();
            _cards.Clear();
        }

        private bool Locate(int instanceId, out CardView? view)
        {
            return _cards.TryGetValue(instanceId, out view) && view != null;
        }

        [Test]
        public void Submit_ReturnsValid_AndSendsEnvelope()
        {
            CommandResult result = _sink.Submit(new EndTurnCommand(0));

            Assert.That(result.IsValid, Is.True);
            Assert.That(_link.Uplinks, Has.Count.EqualTo(1));
        }

        [Test]
        public void Submit_SequenceIncrementsFromOne()
        {
            _sink.Submit(new EndTurnCommand(0));
            _sink.Submit(new EndTurnCommand(0));

            JsonValue first = JsonValue.Parse(_link.Uplinks[0]);
            JsonValue second = JsonValue.Parse(_link.Uplinks[1]);
            Assert.That(first.TryGetMember("seq", out JsonValue seq1) && seq1.IntValue == 1, Is.True);
            Assert.That(second.TryGetMember("seq", out JsonValue seq2) && seq2.IntValue == 2, Is.True);
        }

        [Test]
        public void Submit_EnvelopeCarriesKindAndPayload()
        {
            _sink.Submit(new EndTurnCommand(3));

            JsonValue envelope = JsonValue.Parse(_link.Uplinks[0]);
            Assert.That(envelope.TryGetMember("kind", out JsonValue kind)
                && kind.StringValue == "command", Is.True);
            Assert.That(envelope.TryGetMember("payload", out JsonValue payload), Is.True);
            Assert.That(payload.TryGetMember("type", out JsonValue type)
                && type.StringValue == "EndTurn", Is.True);
            Assert.That(payload.TryGetMember("playerId", out JsonValue playerId)
                && playerId.IntValue == 3, Is.True);
        }

        [Test]
        public void Submit_RegistersPending_AndAuthorityStepResolves()
        {
            _sink.Submit(new EndTurnCommand(0));
            Assert.That(_tracker.PendingCount, Is.EqualTo(1));

            // 权威接受回执（seq=1）→ 登记清空
            var applied = new StepOutcome(StepApplyStatus.Applied, true, 1,
                CommandError.None, string.Empty, System.Array.Empty<JsonValue>());
            _feedback.NotifyStep(applied);
            Assert.That(_tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Submit_PlayCardCommand_ShowsPendingVisualOnCard()
        {
            CardView card = PresentationTestPrefabs.CreateCardViewPrefab();
            _trash.Add(card);
            card.SetData(new CardViewData("测试卡", "d", 1, 2, 3, "art", CardType.Minion, 42));
            _cards[42] = card;

            _sink.Submit(new PlayCardCommand(0, 42, TargetRef.None));

            CardPendingView? pending = card.GetComponent<CardPendingView>();
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending!.IsPending, Is.True);
        }

        [Test]
        public void Submit_EndTurnCommand_DoesNotTouchCardVisuals()
        {
            CardView card = PresentationTestPrefabs.CreateCardViewPrefab();
            _trash.Add(card);
            card.SetData(new CardViewData("测试卡", "d", 1, 2, 3, "art", CardType.Minion, 7));
            _cards[7] = card;

            _sink.Submit(new EndTurnCommand(0));

            Assert.That(card.GetComponent<CardPendingView>(), Is.Null);
        }
    }
}
