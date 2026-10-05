using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    public sealed class CommandResultTests
    {
        [Test]
        public void Valid_IsValidWithNoErrorAndEmptyDetail()
        {
            CommandResult r = CommandResult.Valid();

            Assert.That(r.IsValid, Is.True);
            Assert.That(r.IsInvalid, Is.False);
            Assert.That(r.Error, Is.EqualTo(CommandError.None));
            Assert.That(r.Detail, Is.Empty);
        }

        [Test]
        public void Invalid_CarriesErrorWithEmptyDetailByDefault()
        {
            CommandResult r = CommandResult.Invalid(CommandError.NotEnoughMana);

            Assert.That(r.IsValid, Is.False);
            Assert.That(r.IsInvalid, Is.True);
            Assert.That(r.Error, Is.EqualTo(CommandError.NotEnoughMana));
            Assert.That(r.Detail, Is.Empty);
        }

        [Test]
        public void Invalid_CarriesReadableDetail()
        {
            CommandResult r = CommandResult.Invalid(CommandError.MustTargetTaunt, "必须优先攻击嘲讽随从");

            Assert.That(r.Error, Is.EqualTo(CommandError.MustTargetTaunt));
            Assert.That(r.Detail, Is.EqualTo("必须优先攻击嘲讽随从"));
        }

        [Test]
        public void DifferentErrors_AreDistinguishable()
        {
            CommandResult boardFull = CommandResult.Invalid(CommandError.BoardFull);
            CommandResult targetRequired = CommandResult.Invalid(CommandError.TargetRequired);

            Assert.That(boardFull.Error, Is.Not.EqualTo(targetRequired.Error));
        }

        [Test]
        public void CommandResult_IsValueType()
        {
            Assert.That(typeof(CommandResult).IsValueType, Is.True);
        }
    }
}
