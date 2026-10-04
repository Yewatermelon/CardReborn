using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>
    /// M1-T1：<see cref="Card.Core.Result"/>（无返回值）的成功/失败语义、错误信息与相等性。
    /// </summary>
    public sealed class ResultTests
    {
        [Test]
        public void Success_WhenCreated_ReportsSuccessWithEmptyError()
        {
            Result result = Result.Success();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
            Assert.That(result.ErrorCode, Is.Empty);
            Assert.That(result.ErrorMessage, Is.Empty);
        }

        [Test]
        public void Default_WhenNotInitialized_IsTreatedAsSuccess()
        {
            Result result = default;

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public void Failure_WhenCreated_KeepsErrorCodeAndMessage()
        {
            Result result = Result.Failure("ERROR_NOT_ENOUGH_MANA", "法力不足");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_NOT_ENOUGH_MANA"));
            Assert.That(result.ErrorMessage, Is.EqualTo("法力不足"));
        }

        [Test]
        public void Failure_WhenMessageOmitted_YieldsEmptyMessageNotNull()
        {
            Result result = Result.Failure("ERROR_UNKNOWN");

            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Failure_WhenErrorCodeIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => Result.Failure(null!))!;

            Assert.That(exception.ParamName, Is.EqualTo("errorCode"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Failure_WhenErrorCodeIsBlank_ThrowsArgumentException(string errorCode)
        {
            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => Result.Failure(errorCode))!;

            Assert.That(exception.ParamName, Is.EqualTo("errorCode"));
        }

        [Test]
        public void ToString_WhenSuccess_IsSuccessMarker()
        {
            Assert.That(Result.Success().ToString(), Is.EqualTo("Success"));
        }

        [Test]
        public void ToString_WhenFailureWithMessage_ContainsCodeAndMessage()
        {
            string text = Result.Failure("ERROR_TEST", "说明文本").ToString();

            Assert.That(text, Does.Contain("ERROR_TEST"));
            Assert.That(text, Does.Contain("说明文本"));
        }

        [Test]
        public void ToString_WhenFailureWithoutMessage_OmitsMessagePart()
        {
            string text = Result.Failure("ERROR_TEST").ToString();

            Assert.That(text, Does.Contain("ERROR_TEST"));
            Assert.That(text, Does.Not.Contain("()"));
        }

        [Test]
        public void Equals_WhenSameErrorCodeAndMessage_IsTrue()
        {
            Result left = Result.Failure("ERROR_A", "说明");
            Result right = Result.Failure("ERROR_A", "说明");

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void Equals_WhenDifferentErrorCode_IsFalse()
        {
            Result left = Result.Failure("ERROR_A");
            Result right = Result.Failure("ERROR_B");

            Assert.That(left.Equals(right), Is.False);
        }

        [Test]
        public void Equals_WhenSameErrorCodeButDifferentMessage_IsFalse()
        {
            Result left = Result.Failure("ERROR_A", "说明一");
            Result right = Result.Failure("ERROR_A", "说明二");

            Assert.That(left.Equals(right), Is.False);
        }

        [Test]
        public void Equals_WhenBothSuccess_IsTrue()
        {
            Assert.That(Result.Success().Equals(Result.Success()), Is.True);
        }

        [Test]
        public void Equals_WhenOtherIsNotResult_IsFalse()
        {
            Assert.That(Result.Success().Equals("not-a-result"), Is.False);
        }

        [Test]
        public void EqualityOperators_WhenResultsDiffer_MatchEquals()
        {
            Result success = Result.Success();
            Result failure = Result.Failure("ERROR_A");

            Assert.That(success == Result.Success(), Is.True);
            Assert.That(success != failure, Is.True);
            Assert.That(failure == Result.Failure("ERROR_A"), Is.True);
        }
    }
}
