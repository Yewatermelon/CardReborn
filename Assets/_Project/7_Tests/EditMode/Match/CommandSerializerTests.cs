using System;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T9 命令序列化测试。</summary>
    [TestFixture]
    public sealed class CommandSerializerTests
    {
        [Test]
        public void Serialize_PlayCardCommand_RoundTrips()
        {
            var cmd = new PlayCardCommand(0, 5, TargetRef.ForHero(1));

            string json = CommandSerializer.Serialize(cmd);
            IGameCommand back = CommandSerializer.Deserialize(json);

            Assert.That(back, Is.TypeOf<PlayCardCommand>());
            var typed = (PlayCardCommand)back;
            Assert.That(typed.PlayerId, Is.EqualTo(0));
            Assert.That(typed.CardInstanceId, Is.EqualTo(5));
            Assert.That(typed.Target, Is.EqualTo(TargetRef.ForHero(1)));
        }

        [Test]
        public void Serialize_AttackCommand_RoundTrips()
        {
            var cmd = new AttackCommand(1, 7, TargetRef.ForMinion(12));

            string json = CommandSerializer.Serialize(cmd);
            IGameCommand back = CommandSerializer.Deserialize(json);

            Assert.That(back, Is.TypeOf<AttackCommand>());
            var typed = (AttackCommand)back;
            Assert.That(typed.PlayerId, Is.EqualTo(1));
            Assert.That(typed.AttackerInstanceId, Is.EqualTo(7));
            Assert.That(typed.Target, Is.EqualTo(TargetRef.ForMinion(12)));
        }

        [Test]
        public void Serialize_UseHeroPowerCommand_RoundTrips()
        {
            var cmd = new UseHeroPowerCommand(0, TargetRef.ForHero(1));

            string json = CommandSerializer.Serialize(cmd);
            IGameCommand back = CommandSerializer.Deserialize(json);

            Assert.That(back, Is.TypeOf<UseHeroPowerCommand>());
            var typed = (UseHeroPowerCommand)back;
            Assert.That(typed.PlayerId, Is.EqualTo(0));
            Assert.That(typed.Target, Is.EqualTo(TargetRef.ForHero(1)));
        }

        [Test]
        public void Serialize_UseHeroPowerCommand_WithNoTarget_RoundTrips()
        {
            var cmd = new UseHeroPowerCommand(1, TargetRef.None);

            string json = CommandSerializer.Serialize(cmd);
            IGameCommand back = CommandSerializer.Deserialize(json);

            Assert.That(back, Is.TypeOf<UseHeroPowerCommand>());
            var typed = (UseHeroPowerCommand)back;
            Assert.That(typed.Target.IsNone, Is.True);
        }

        [Test]
        public void Serialize_EndTurnCommand_RoundTrips()
        {
            var cmd = new EndTurnCommand(0);

            string json = CommandSerializer.Serialize(cmd);
            IGameCommand back = CommandSerializer.Deserialize(json);

            Assert.That(back, Is.TypeOf<EndTurnCommand>());
            var typed = (EndTurnCommand)back;
            Assert.That(typed.PlayerId, Is.EqualTo(0));
        }

        [Test]
        public void Deserialize_UnknownType_Throws()
        {
            const string json = "{ \"type\": \"FlyCard\", \"playerId\": 0 }";

            try
            {
                CommandSerializer.Deserialize(json);
                Assert.Fail("应抛 ArgumentException");
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
