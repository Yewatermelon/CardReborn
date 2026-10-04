using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>
    /// M1-T1：<see cref="Card.Core.Result{T}"/>（带返回值）的取值语义与相等性。
    /// </summary>
    public sealed class ResultOfValueTests
    {
        [Test]
        public void Success_WhenCreated_ExposesValue()
        {
            Result<int> result = Result<int>.Success(42);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
            Assert.That(result.Value, Is.EqualTo(42));
            Assert.That(result.ErrorCode, Is.Empty);
        }

        [Test]
        public void Success_WhenTryGetValue_ReturnsTrueAndValue()
        {
            Result<int> result = Result<int>.Success(7);

            bool found = result.TryGetValue(out int value);

            Assert.That(found, Is.True);
            Assert.That(value, Is.EqualTo(7));
        }

        [Test]
        public void Success_WhenReferenceValueIsNull_IsStillSuccess()
        {
            Result<string> result = Result<string>.Success(null!);

            bool found = result.TryGetValue(out string value);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(found, Is.True);
            Assert.That(value, Is.Null);
        }

        [Test]
        public void Failure_WhenCreated_KeepsErrorCodeAndMessage()
        {
            Result<int> result = Result<int>.Failure("ERROR_TARGET_REQUIRED", "该卡需要指定目标");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("ERROR_TARGET_REQUIRED"));
            Assert.That(result.ErrorMessage, Is.EqualTo("该卡需要指定目标"));
        }

        [Test]
        public void Failure_WhenAccessingValue_ThrowsInvalidOperationException()
        {
            Result<int> result = Result<int>.Failure("ERROR_TARGET_REQUIRED");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => _ = result.Value)!;

            Assert.That(exception.Message, Does.Contain("ERROR_TARGET_REQUIRED"));
        }

        [Test]
        public void Failure_WhenTryGetValue_ReturnsFalseAndDefaultValue()
        {
            Result<int> result = Result<int>.Failure("ERROR_TARGET_REQUIRED");

            bool found = result.TryGetValue(out int value);

            Assert.That(found, Is.False);
            Assert.That(value, Is.EqualTo(0));
        }

        [Test]
        public void Failure_WhenReferenceType_ExposesNullAndEmptyMessage()
        {
            Result<string> result = Result<string>.Failure("ERROR_EMPTY_LIBRARY");

            bool found = result.TryGetValue(out string value);

            Assert.That(found, Is.False);
            Assert.That(value, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Failure_WhenErrorCodeIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!))!;

            Assert.That(exception.ParamName, Is.EqualTo("errorCode"));
        }

        [TestCase("")]
        [TestCase("  ")]
        public void Failure_WhenErrorCodeIsBlank_ThrowsArgumentException(string errorCode)
        {
            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => Result<int>.Failure(errorCode))!;

            Assert.That(exception.ParamName, Is.EqualTo("errorCode"));
        }

        [Test]
        public void Equals_WhenSameSuccessValue_IsTrue()
        {
            Result<int> left = Result<int>.Success(5);
            Result<int> right = Result<int>.Success(5);

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left == right, Is.True);
        }

        [Test]
        public void Equals_WhenDifferentSuccessValue_IsFalse()
        {
            Result<int> left = Result<int>.Success(5);
            Result<int> right = Result<int>.Success(6);

            Assert.That(left.Equals(right), Is.False);
            Assert.That(left != right, Is.True);
        }

        [Test]
        public void Equals_WhenSameFailure_IsTrueRegardlessOfValue()
        {
            Result<int> left = Result<int>.Failure("ERROR_A", "说明");
            Result<int> right = Result<int>.Failure("ERROR_A", "说明");

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void Equals_WhenSuccessAndFailure_IsFalse()
        {
            Result<int> success = Result<int>.Success(0);
            Result<int> failure = Result<int>.Failure("ERROR_A");

            Assert.That(success.Equals(failure), Is.False);
        }

        [Test]
        public void ToString_WhenFailureWithMessage_ContainsCodeAndMessage()
        {
            string text = Result<int>.Failure("ERROR_A", "说明").ToString();

            Assert.That(text, Does.Contain("ERROR_A"));
            Assert.That(text, Does.Contain("说明"));
        }

        [Test]
        public void ToString_WhenSuccess_ContainsValue()
        {
            string text = Result<int>.Success(42).ToString();

            Assert.That(text, Does.Contain("42"));
        }
    }
}
