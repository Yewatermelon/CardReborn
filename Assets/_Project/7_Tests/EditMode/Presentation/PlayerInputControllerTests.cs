using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Card.Domain.Match;
using Card.Presentation.Battle.Input;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class PlayerInputControllerTests
    {
        private GameObject _root = null!;
        private PlayerInputController _controller = null!;
        private StubCommandSink _sink = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PlayerInputController_Test");
            _controller = _root.AddComponent<PlayerInputController>();
            _controller._localPlayerId = 7;
            _sink = new StubCommandSink();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        private sealed class StubCommandSink : ICommandSink
        {
            public readonly List<IGameCommand> Received = new List<IGameCommand>();
            public CommandResult NextResult = CommandResult.Valid();

            public CommandResult Submit(IGameCommand command)
            {
                Received.Add(command);
                return NextResult;
            }
        }

        [Test]
        public void NotifyEndTurn_WithoutInitialize_ThrowsInvalidOperationException()
        {
            Assert.That(
                () => _controller.NotifyEndTurnClicked(),
                Throws.InvalidOperationException);
        }

        [Test]
        public void NotifyHandCard_WithoutInitialize_ThrowsInvalidOperationException()
        {
            Assert.That(
                () => _controller.NotifyHandCardClicked(1),
                Throws.InvalidOperationException);
        }

        [Test]
        public void NotifyEndTurn_SubmitsEndTurnCommandWithLocalPlayerId()
        {
            _controller.Initialize(_sink);

            _controller.NotifyEndTurnClicked();

            Assert.That(_sink.Received.Count, Is.EqualTo(1));
            Assert.That(_sink.Received[0], Is.TypeOf<EndTurnCommand>());
            Assert.That(((EndTurnCommand)_sink.Received[0]).PlayerId, Is.EqualTo(7));
        }

        [Test]
        public void NotifyHandCard_SubmitsPlayCardCommandWithNoneTarget()
        {
            _controller.Initialize(_sink);

            _controller.NotifyHandCardClicked(42);

            Assert.That(_sink.Received.Count, Is.EqualTo(1));
            Assert.That(_sink.Received[0], Is.TypeOf<PlayCardCommand>());
            PlayCardCommand cmd = (PlayCardCommand)_sink.Received[0];
            Assert.That(cmd.PlayerId, Is.EqualTo(7));
            Assert.That(cmd.CardInstanceId, Is.EqualTo(42));
            Assert.That(cmd.Target, Is.EqualTo(TargetRef.None));
        }

        [Test]
        public void NotifyBoardMinion_SubmitsAttackCommandWithHeroTarget()
        {
            _controller.Initialize(_sink);

            _controller.NotifyBoardMinionClicked(11, 1);

            Assert.That(_sink.Received.Count, Is.EqualTo(1));
            Assert.That(_sink.Received[0], Is.TypeOf<AttackCommand>());
            AttackCommand cmd = (AttackCommand)_sink.Received[0];
            Assert.That(cmd.PlayerId, Is.EqualTo(7));
            Assert.That(cmd.AttackerInstanceId, Is.EqualTo(11));
            Assert.That(cmd.Target, Is.EqualTo(TargetRef.ForHero(1)));
        }

        [Test]
        public void NotifyHeroPower_SubmitsUseHeroPowerCommandWithNoneTarget()
        {
            _controller.Initialize(_sink);

            _controller.NotifyHeroPowerClicked();

            Assert.That(_sink.Received.Count, Is.EqualTo(1));
            Assert.That(_sink.Received[0], Is.TypeOf<UseHeroPowerCommand>());
            UseHeroPowerCommand cmd = (UseHeroPowerCommand)_sink.Received[0];
            Assert.That(cmd.PlayerId, Is.EqualTo(7));
            Assert.That(cmd.Target, Is.EqualTo(TargetRef.None));
        }

        [Test]
        public void Submit_Valid_FiresAcceptedNotRejected()
        {
            _controller.Initialize(_sink);
            _sink.NextResult = CommandResult.Valid();
            int accepted = 0;
            int rejected = 0;
            _controller.CommandAccepted += () => accepted++;
            _controller.CommandRejected += _ => rejected++;

            _controller.NotifyEndTurnClicked();

            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(rejected, Is.EqualTo(0));
        }

        [Test]
        public void Submit_Invalid_FiresRejectedWithErrorNotAccepted()
        {
            _controller.Initialize(_sink);
            _sink.NextResult = CommandResult.Invalid(CommandError.NotYourTurn);
            int accepted = 0;
            CommandError? rejectedError = null;
            _controller.CommandAccepted += () => accepted++;
            _controller.CommandRejected += e => rejectedError = e;

            _controller.NotifyEndTurnClicked();

            Assert.That(accepted, Is.EqualTo(0));
            Assert.That(rejectedError, Is.EqualTo(CommandError.NotYourTurn));
        }

        [Test]
        public void Submit_InvalidNotEnoughMana_CarriesErrorCode()
        {
            _controller.Initialize(_sink);
            _sink.NextResult = CommandResult.Invalid(CommandError.NotEnoughMana, "need 4");
            CommandError? rejectedError = null;
            _controller.CommandRejected += e => rejectedError = e;

            _controller.NotifyHandCardClicked(1);

            Assert.That(rejectedError, Is.EqualTo(CommandError.NotEnoughMana));
        }

        [Test]
        public void Initialize_NullSink_ThrowsArgumentNullException()
        {
            Assert.That(
                () => _controller.Initialize(null!),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("sink"));
        }
    }
}
