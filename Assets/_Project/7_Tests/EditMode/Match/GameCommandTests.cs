using System.Linq;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    public sealed class GameCommandTests
    {
        [Test]
        public void PlayCardCommand_StoresPlayerCardAndTarget()
        {
            TargetRef target = TargetRef.ForMinion(7);

            PlayCardCommand cmd = new PlayCardCommand(1, 42, target);

            Assert.That(cmd.PlayerId, Is.EqualTo(1));
            Assert.That(cmd.CardInstanceId, Is.EqualTo(42));
            Assert.That(cmd.Target, Is.EqualTo(target));
        }

        [Test]
        public void PlayCardCommand_IsGameCommandAndAllowsNoneTarget()
        {
            IGameCommand cmd = new PlayCardCommand(0, 5, TargetRef.None);

            Assert.That(cmd.PlayerId, Is.EqualTo(0));
            Assert.That(((PlayCardCommand)cmd).Target.IsNone, Is.True);
        }

        [Test]
        public void AttackCommand_StoresAttackerAndMinionTarget()
        {
            AttackCommand cmd = new AttackCommand(0, 3, TargetRef.ForMinion(9));

            Assert.That(cmd.PlayerId, Is.EqualTo(0));
            Assert.That(cmd.AttackerInstanceId, Is.EqualTo(3));
            Assert.That(cmd.Target.Kind, Is.EqualTo(TargetKind.Minion));
            Assert.That(cmd.Target.TargetId, Is.EqualTo(9));
        }

        [Test]
        public void AttackCommand_IsGameCommandAndAllowsHeroTarget()
        {
            IGameCommand cmd = new AttackCommand(1, 4, TargetRef.ForHero(0));

            Assert.That(cmd.PlayerId, Is.EqualTo(1));
            Assert.That(((AttackCommand)cmd).Target.Kind, Is.EqualTo(TargetKind.Hero));
        }

        [Test]
        public void UseHeroPowerCommand_IsGameCommandAndStoresTarget()
        {
            IGameCommand cmd = new UseHeroPowerCommand(0, TargetRef.ForMinion(11));

            Assert.That(cmd.PlayerId, Is.EqualTo(0));
            Assert.That(((UseHeroPowerCommand)cmd).Target.TargetId, Is.EqualTo(11));
        }

        [Test]
        public void UseHeroPowerCommand_AllowsNoneTarget()
        {
            UseHeroPowerCommand cmd = new UseHeroPowerCommand(1, TargetRef.None);

            Assert.That(cmd.Target.IsNone, Is.True);
        }

        [Test]
        public void EndTurnCommand_IsGameCommandWithPlayerId()
        {
            IGameCommand cmd = new EndTurnCommand(1);

            Assert.That(cmd.PlayerId, Is.EqualTo(1));
        }

        [Test]
        public void TargetRef_None_HasNoneKindAndZeroId()
        {
            TargetRef r = TargetRef.None;

            Assert.That(r.Kind, Is.EqualTo(TargetKind.None));
            Assert.That(r.IsNone, Is.True);
            Assert.That(r.TargetId, Is.EqualTo(0));
        }

        [Test]
        public void TargetRef_ForMinion_StoresInstanceId()
        {
            TargetRef r = TargetRef.ForMinion(23);

            Assert.That(r.Kind, Is.EqualTo(TargetKind.Minion));
            Assert.That(r.IsNone, Is.False);
            Assert.That(r.TargetId, Is.EqualTo(23));
        }

        [Test]
        public void TargetRef_ForHero_StoresPlayerId()
        {
            TargetRef r = TargetRef.ForHero(1);

            Assert.That(r.Kind, Is.EqualTo(TargetKind.Hero));
            Assert.That(r.IsNone, Is.False);
            Assert.That(r.TargetId, Is.EqualTo(1));
        }

        [Test]
        public void TargetRef_EqualWhenSameKindAndId()
        {
            Assert.That(TargetRef.ForMinion(5), Is.EqualTo(TargetRef.ForMinion(5)));
            Assert.That(TargetRef.ForMinion(5), Is.Not.EqualTo(TargetRef.ForMinion(6)));
            Assert.That(TargetRef.ForMinion(5), Is.Not.EqualTo(TargetRef.ForHero(5)));
        }

        [Test]
        public void TargetRef_ObjectEquals_HandlesBoxedNullAndOtherTypes()
        {
            TargetRef r = TargetRef.ForMinion(5);

            Assert.That(r.Equals((object)TargetRef.ForMinion(5)), Is.True);
            Assert.That(r.Equals(null), Is.False);
            Assert.That(r.Equals(TargetRef.ForHero(5)), Is.False);
            Assert.That(r.Equals("not a target"), Is.False);
        }

        [Test]
        public void TargetRef_GetHashCode_EqualForEqualRefs()
        {
            Assert.That(TargetRef.ForMinion(5).GetHashCode(),
                Is.EqualTo(TargetRef.ForMinion(5).GetHashCode()));
            Assert.That(TargetRef.None.GetHashCode(),
                Is.EqualTo(TargetRef.None.GetHashCode()));
        }

        [Test]
        public void TargetRef_EqualityOperators_Work()
        {
            Assert.That(TargetRef.ForHero(1) == TargetRef.ForHero(1), Is.True);
            Assert.That(TargetRef.ForMinion(1) != TargetRef.ForMinion(2), Is.True);
            Assert.That(TargetRef.ForMinion(1) == TargetRef.ForHero(1), Is.False);
            Assert.That(TargetRef.ForMinion(1) != TargetRef.ForMinion(1), Is.False);
        }

        [Test]
        public void Commands_AreValueTypesWithNoPublicPropertySetter()
        {
            System.Type[] commandTypes =
            {
                typeof(PlayCardCommand), typeof(AttackCommand),
                typeof(UseHeroPowerCommand), typeof(EndTurnCommand)
            };

            foreach (System.Type type in commandTypes)
            {
                Assert.That(type.IsValueType, Is.True, type.Name);
                Assert.That(type.GetProperties().All(p => !p.CanWrite), Is.True, type.Name);
            }
        }
    }
}
