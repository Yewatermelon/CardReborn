using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 不带返回值的结果：成功，或失败（含错误码与说明）。
    /// 约定：可预期的业务失败用返回值表达，不用异常（见 Docs/03 第 11 节）。
    /// </summary>
    public readonly struct Result : IEquatable<Result>
    {
        private readonly string _errorCode;
        private readonly string _errorMessage;

        private Result(string errorCode, string errorMessage)
        {
            _errorCode = errorCode;
            _errorMessage = errorMessage;
        }

        /// <summary>是否成功。</summary>
        public bool IsSuccess
        {
            get { return _errorCode == null; }
        }

        /// <summary>是否失败。</summary>
        public bool IsFailure
        {
            get { return _errorCode != null; }
        }

        /// <summary>失败时的错误码；成功时为空字符串。</summary>
        public string ErrorCode
        {
            get { return _errorCode ?? string.Empty; }
        }

        /// <summary>失败时的补充说明；未提供时为空字符串。</summary>
        public string ErrorMessage
        {
            get { return _errorMessage ?? string.Empty; }
        }

        /// <summary>创建成功结果。</summary>
        public static Result Success()
        {
            return default;
        }

        /// <summary>创建失败结果；<paramref name="errorCode"/> 不可为 null、空或空白。</summary>
        public static Result Failure(string errorCode, string errorMessage = "")
        {
            Guard.NotNullOrWhiteSpace(errorCode, nameof(errorCode));
            return new Result(errorCode, errorMessage ?? string.Empty);
        }

        /// <inheritdoc />
        public bool Equals(Result other)
        {
            return string.Equals(ErrorCode, other.ErrorCode, StringComparison.Ordinal)
                && string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Result other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (ErrorCode.GetHashCode() * 397) ^ ErrorMessage.GetHashCode();
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (IsSuccess)
            {
                return "Success";
            }

            return ErrorMessage.Length == 0
                ? "Failure: " + ErrorCode
                : "Failure: " + ErrorCode + " (" + ErrorMessage + ")";
        }

        public static bool operator ==(Result left, Result right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Result left, Result right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 带返回值的结果：成功时可取值，失败时携带错误码与说明。
    /// 与 <see cref="Result"/> 同属一个类型族，故放在同一文件（见任务卡 M1-T1 的文件组织说明）。
    /// </summary>
    public readonly struct Result<T> : IEquatable<Result<T>>
    {
        private readonly T _value;
        private readonly string _errorCode;
        private readonly string _errorMessage;

        private Result(T value, string errorCode, string errorMessage)
        {
            _value = value;
            _errorCode = errorCode;
            _errorMessage = errorMessage;
        }

        /// <summary>是否成功。</summary>
        public bool IsSuccess
        {
            get { return _errorCode == null; }
        }

        /// <summary>是否失败。</summary>
        public bool IsFailure
        {
            get { return _errorCode != null; }
        }

        /// <summary>失败时的错误码；成功时为空字符串。</summary>
        public string ErrorCode
        {
            get { return _errorCode ?? string.Empty; }
        }

        /// <summary>失败时的补充说明；未提供时为空字符串。</summary>
        public string ErrorMessage
        {
            get { return _errorMessage ?? string.Empty; }
        }

        /// <summary>成功时的返回值；失败时访问会抛 <see cref="InvalidOperationException"/>。</summary>
        public T Value
        {
            get
            {
                if (IsFailure)
                {
                    throw new InvalidOperationException("失败的结果不包含值：" + ErrorCode);
                }

                return _value;
            }
        }

        /// <summary>创建成功结果；值允许为 null（成功与否只由错误码决定）。</summary>
        public static Result<T> Success(T value)
        {
            return new Result<T>(value, null, string.Empty);
        }

        /// <summary>创建失败结果；<paramref name="errorCode"/> 不可为 null、空或空白。</summary>
        public static Result<T> Failure(string errorCode, string errorMessage = "")
        {
            Guard.NotNullOrWhiteSpace(errorCode, nameof(errorCode));
            return new Result<T>(default, errorCode, errorMessage ?? string.Empty);
        }

        /// <summary>尝试取值；失败时返回 false 并输出 <c>default</c>。</summary>
        public bool TryGetValue(out T value)
        {
            value = IsSuccess ? _value : default;
            return IsSuccess;
        }

        /// <inheritdoc />
        public bool Equals(Result<T> other)
        {
            if (IsFailure || other.IsFailure)
            {
                return string.Equals(ErrorCode, other.ErrorCode, StringComparison.Ordinal)
                    && string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal);
            }

            return EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Result<T> other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (ErrorCode.GetHashCode() * 397) ^ ErrorMessage.GetHashCode();
                if (IsSuccess)
                {
                    hash = (hash * 397) ^ EqualityComparer<T>.Default.GetHashCode(_value);
                }

                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (IsFailure)
            {
                return ErrorMessage.Length == 0
                    ? "Failure: " + ErrorCode
                    : "Failure: " + ErrorCode + " (" + ErrorMessage + ")";
            }

            return "Success: " + _value;
        }

        public static bool operator ==(Result<T> left, Result<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Result<T> left, Result<T> right)
        {
            return !left.Equals(right);
        }
    }
}
