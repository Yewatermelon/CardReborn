using System;
using NUnit.Framework;
using UnityEngine;
using Card.Domain.Match;
using Card.Presentation.Battle.Targeting;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    internal sealed class TargetingControllerTests : TargetingTestFixture
    {
        [Test]
        public void Begin_WithoutInitialize_ThrowsInvalidOperationException()
        {
            Assert.That(
                () => Controller.BeginPlayCardTargeting(1, FixedOrigin(Vector2.zero)),
                Throws.InvalidOperationException);
        }

        [Test]
        public void Begin_NullOrigin_ThrowsArgumentNullException()
        {
            Initialize();
            Assert.That(
                () => Controller.BeginPlayCardTargeting(1, null!),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("originScreenProvider"));
        }

        [Test]
        public void Begin_SetsIsTargeting_AndShowsArrow()
        {
            Initialize();

            Controller.BeginPlayCardTargeting(9, FixedOrigin(Vector2.zero));

            Assert.That(Controller.IsTargeting, Is.True);
            Assert.That(Arrow.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Tick_NotTargeting_DoesNothing()
        {
            Initialize();
            Input.IsCancelPressed = true;
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(Sink.Received.Count, Is.EqualTo(0));
            Assert.That(Arrow.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Tick_CancelPressed_CancelsWithoutSubmit()
        {
            Initialize();
            int cancelled = 0;
            Controller.TargetingCancelled += () => cancelled++;
            Controller.BeginPlayCardTargeting(9, FixedOrigin(Vector2.zero));
            Input.IsCancelPressed = true;

            Controller.Tick();

            Assert.That(Controller.IsTargeting, Is.False);
            Assert.That(Arrow.gameObject.activeSelf, Is.False);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(Sink.Received.Count, Is.EqualTo(0));
        }

        [Test]
        public void CancelTargeting_WhenNotTargeting_IsNoOp()
        {
            Initialize();
            int cancelled = 0;
            Controller.TargetingCancelled += () => cancelled++;

            Controller.CancelTargeting();

            Assert.That(cancelled, Is.EqualTo(0));
        }

        [Test]
        public void Tick_ConfirmHit_PlayCard_SubmitsWithPickedTarget()
        {
            Initialize();
            Controller.BeginPlayCardTargeting(9, FixedOrigin(Vector2.zero));
            Picker.Hit = true;
            Picker.Picked = TargetRef.ForMinion(77);
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(Sink.Received.Count, Is.EqualTo(1));
            PlayCardCommand cmd = (PlayCardCommand)Sink.Received[0];
            Assert.That(cmd.PlayerId, Is.EqualTo(3));
            Assert.That(cmd.CardInstanceId, Is.EqualTo(9));
            Assert.That(cmd.Target, Is.EqualTo(TargetRef.ForMinion(77)));
            Assert.That(Controller.IsTargeting, Is.False);
            Assert.That(Arrow.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Tick_ConfirmHit_Attack_SubmitsWithPickedTarget()
        {
            Initialize();
            Controller.BeginAttackTargeting(21, FixedOrigin(Vector2.zero));
            Picker.Hit = true;
            Picker.Picked = TargetRef.ForHero(1);
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(Sink.Received.Count, Is.EqualTo(1));
            AttackCommand cmd = (AttackCommand)Sink.Received[0];
            Assert.That(cmd.PlayerId, Is.EqualTo(3));
            Assert.That(cmd.AttackerInstanceId, Is.EqualTo(21));
            Assert.That(cmd.Target, Is.EqualTo(TargetRef.ForHero(1)));
        }

        [Test]
        public void Tick_ConfirmHit_HeroPower_SubmitsWithPickedTarget()
        {
            Initialize();
            Controller.BeginHeroPowerTargeting(FixedOrigin(Vector2.zero));
            Picker.Hit = true;
            Picker.Picked = TargetRef.ForMinion(55);
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(Sink.Received.Count, Is.EqualTo(1));
            UseHeroPowerCommand cmd = (UseHeroPowerCommand)Sink.Received[0];
            Assert.That(cmd.PlayerId, Is.EqualTo(3));
            Assert.That(cmd.Target, Is.EqualTo(TargetRef.ForMinion(55)));
        }

        [Test]
        public void Tick_ConfirmNoHit_StaysTargeting_NoSubmit()
        {
            Initialize();
            Controller.BeginPlayCardTargeting(9, FixedOrigin(Vector2.zero));
            Picker.Hit = false;
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(Controller.IsTargeting, Is.True);
            Assert.That(Sink.Received.Count, Is.EqualTo(0));
        }

        [Test]
        public void Tick_SinkInvalid_FiresRejectedWithError_AndExitsTargeting()
        {
            Initialize();
            Sink.NextResult = CommandResult.Invalid(CommandError.MustTargetTaunt);
            CommandError? rejected = null;
            int accepted = 0;
            Controller.CommandRejected += e => rejected = e;
            Controller.CommandAccepted += () => accepted++;
            Controller.BeginAttackTargeting(21, FixedOrigin(Vector2.zero));
            Picker.Hit = true;
            Picker.Picked = TargetRef.ForHero(1);
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(rejected, Is.EqualTo(CommandError.MustTargetTaunt));
            Assert.That(accepted, Is.EqualTo(0));
            Assert.That(Controller.IsTargeting, Is.False);
        }

        [Test]
        public void Tick_SinkValid_FiresAccepted_NotRejected()
        {
            Initialize();
            int accepted = 0;
            int rejected = 0;
            Controller.CommandAccepted += () => accepted++;
            Controller.CommandRejected += _ => rejected++;
            Controller.BeginHeroPowerTargeting(FixedOrigin(Vector2.zero));
            Picker.Hit = true;
            Picker.Picked = TargetRef.ForMinion(5);
            Input.IsConfirmPressed = true;

            Controller.Tick();

            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(rejected, Is.EqualTo(0));
        }

        [Test]
        public void Begin_WhileTargeting_CancelsPrevious_ThenStartsNew()
        {
            Initialize();
            int cancelled = 0;
            Controller.TargetingCancelled += () => cancelled++;
            Controller.BeginPlayCardTargeting(9, FixedOrigin(Vector2.zero));

            Controller.BeginAttackTargeting(21, FixedOrigin(Vector2.zero));

            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(Controller.IsTargeting, Is.True);

            Picker.Hit = true;
            Picker.Picked = TargetRef.ForHero(0);
            Input.IsConfirmPressed = true;
            Controller.Tick();

            Assert.That(Sink.Received.Count, Is.EqualTo(1));
            Assert.That(Sink.Received[0], Is.TypeOf<AttackCommand>());
        }

        [Test]
        public void Tick_ArrowFollowsOriginProvider_NotSnapshot()
        {
            Initialize();
            Vector2 origin = new Vector2(0f, 0f);
            Controller.BeginPlayCardTargeting(9, () => origin);
            Input.PointerScreenPosition = new Vector2(0f, 0f);

            Controller.Tick();
            Vector2 first = Arrow._line.anchoredPosition;

            origin = new Vector2(200f, 0f);
            Controller.Tick();
            Vector2 second = Arrow._line.anchoredPosition;

            Assert.That(first.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(second.x, Is.EqualTo(100f).Within(0.01f), "origin 移动后箭头中点应同步移动");
        }

        [Test]
        public void Tick_UpdatesArrowEndpoints_FromOriginToPointer()
        {
            Initialize();
            Controller.BeginPlayCardTargeting(9, FixedOrigin(new Vector2(0f, 0f)));
            Input.PointerScreenPosition = new Vector2(100f, 0f);

            Controller.Tick();

            Assert.That(Arrow._line.anchoredPosition.x, Is.EqualTo(50f).Within(0.01f));
            Assert.That(Arrow._line.sizeDelta.x, Is.EqualTo(100f).Within(0.01f));
        }
    }
}
