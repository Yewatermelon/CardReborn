using System;
using System.Collections.Generic;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>
    /// M1-T1：<see cref="Card.Core.Guard"/> 的参数契约校验行为。
    /// 契约被违反属"程序员错误"，因此抛 BCL 异常而不是返回失败结果。
    /// </summary>
    public sealed class GuardTests
    {
        [Test]
        public void NotNull_WhenNull_ThrowsArgumentNullExceptionWithParamName()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => Guard.NotNull<string>(null!, "value"))!;

            Assert.That(exception.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void NotNull_WhenValue_ReturnsSameInstance()
        {
            object value = new object();

            Assert.That(Guard.NotNull(value, "value"), Is.SameAs(value));
        }

        [Test]
        public void NotNullOrWhiteSpace_WhenNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => Guard.NotNullOrWhiteSpace(null!, "text"))!;

            Assert.That(exception.ParamName, Is.EqualTo("text"));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        public void NotNullOrWhiteSpace_WhenBlank_ThrowsArgumentException(string text)
        {
            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace(text, "text"))!;

            Assert.That(exception.ParamName, Is.EqualTo("text"));
        }

        [Test]
        public void NotNullOrWhiteSpace_WhenValid_ReturnsSameString()
        {
            const string text = "MAGE_FIREBALL";

            Assert.That(Guard.NotNullOrWhiteSpace(text, "text"), Is.SameAs(text));
        }

        [Test]
        public void NotNullOrEmpty_WhenNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => Guard.NotNullOrEmpty<string>(null!, "items"))!;

            Assert.That(exception.ParamName, Is.EqualTo("items"));
        }

        [Test]
        public void NotNullOrEmpty_WhenEmpty_ThrowsArgumentException()
        {
            IReadOnlyCollection<string> items = new List<string>();

            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(items, "items"))!;

            Assert.That(exception.ParamName, Is.EqualTo("items"));
        }

        [Test]
        public void NotNullOrEmpty_WhenHasItems_ReturnsSameInstance()
        {
            IReadOnlyCollection<string> items = new List<string> { "MAGE_FIREBALL" };

            Assert.That(Guard.NotNullOrEmpty(items, "items"), Is.SameAs(items));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void Positive_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeException(int value)
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Positive(value, "cost"))!;

            Assert.That(exception.ParamName, Is.EqualTo("cost"));
        }

        [TestCase(1)]
        [TestCase(int.MaxValue)]
        public void Positive_WhenValueIsPositive_ReturnsValue(int value)
        {
            Assert.That(Guard.Positive(value, "cost"), Is.EqualTo(value));
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NotNegative_WhenValueIsNegative_ThrowsArgumentOutOfRangeException(int value)
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotNegative(value, "damage"))!;

            Assert.That(exception.ParamName, Is.EqualTo("damage"));
        }

        [TestCase(0)]
        [TestCase(int.MaxValue)]
        public void NotNegative_WhenValueIsZeroOrPositive_ReturnsValue(int value)
        {
            Assert.That(Guard.NotNegative(value, "damage"), Is.EqualTo(value));
        }

        [Test]
        public void InRange_WhenValueEqualsMinInclusive_ReturnsValue()
        {
            Assert.That(Guard.InRange(5, 5, 10, "index"), Is.EqualTo(5));
        }

        [Test]
        public void InRange_WhenValueInsideRange_ReturnsValue()
        {
            Assert.That(Guard.InRange(7, 5, 10, "index"), Is.EqualTo(7));
        }

        [TestCase(10)]
        [TestCase(11)]
        public void InRange_WhenValueAtOrAboveMaxExclusive_ThrowsArgumentOutOfRangeException(int value)
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => Guard.InRange(value, 5, 10, "index"))!;

            Assert.That(exception.ParamName, Is.EqualTo("index"));
        }

        [Test]
        public void InRange_WhenValueBelowMin_ThrowsArgumentOutOfRangeException()
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => Guard.InRange(4, 5, 10, "index"))!;

            Assert.That(exception.ParamName, Is.EqualTo("index"));
        }

        [Test]
        public void InRange_WhenRangeIsEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Guard.InRange(5, 5, 5, "index"));
        }

        [Test]
        public void InRange_WhenMinGreaterThanMax_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Guard.InRange(5, 10, 5, "index"));
        }

        [Test]
        public void Require_WhenConditionIsFalse_ThrowsArgumentExceptionWithMessage()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Guard.Require(false, "state", "回合阶段不匹配"))!;

            Assert.That(exception.ParamName, Is.EqualTo("state"));
            Assert.That(exception.Message, Does.Contain("回合阶段不匹配"));
        }

        [Test]
        public void Require_WhenConditionIsTrue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Guard.Require(true, "state", "不会触发"));
        }
    }
}
