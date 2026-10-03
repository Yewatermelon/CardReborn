using System;
using System.Collections.Generic;

namespace Card.Core
{
    /// <summary>
    /// 公共 API 的入参契约校验。
    /// 约定：契约被违反（传 null、越界等）属程序员错误 → 抛 BCL 异常；
    /// 可预期的业务失败 → 返回 <see cref="Result"/>（见 Docs/03 第 11 节）。
    /// </summary>
    public static class Guard
    {
        /// <summary>校验引用类型参数不为 null，并原样返回。</summary>
        public static T NotNull<T>(T value, string paramName) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName, "不能为 null。");
            }

            return value;
        }

        /// <summary>校验字符串不为 null、空或纯空白，并原样返回。</summary>
        public static string NotNullOrWhiteSpace(string value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName, "不能为 null。");
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("不能为空或纯空白字符串。", paramName);
            }

            return value;
        }

        /// <summary>校验集合不为 null 且至少含一个元素，并原样返回。</summary>
        public static IReadOnlyCollection<T> NotNullOrEmpty<T>(
            IReadOnlyCollection<T> items,
            string paramName)
        {
            if (items == null)
            {
                throw new ArgumentNullException(paramName, "不能为 null。");
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("集合不能为空。", paramName);
            }

            return items;
        }

        /// <summary>校验整数为正数（&gt; 0），并原样返回。</summary>
        public static int Positive(int value, string paramName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "必须为正数。");
            }

            return value;
        }

        /// <summary>校验整数非负（&gt;= 0），并原样返回。</summary>
        public static int NotNegative(int value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "不能为负数。");
            }

            return value;
        }

        /// <summary>校验整数落在 [minInclusive, maxExclusive) 区间内，并原样返回。</summary>
        public static int InRange(int value, int minInclusive, int maxExclusive, string paramName)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentException(
                    "区间必须非空：minInclusive 必须小于 maxExclusive。",
                    nameof(minInclusive));
            }

            if (value < minInclusive || value >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "必须落在 [" + minInclusive + ", " + maxExclusive + ") 区间内。");
            }

            return value;
        }

        /// <summary>校验任意前置条件；不满足时抛 <see cref="ArgumentException"/>。</summary>
        public static void Require(bool condition, string paramName, string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message, paramName);
            }
        }
    }
}
