using System;

namespace Card.Domain.Config
{
    /// <summary>
    /// 配置表文本到枚举的严格解析原语（供导入器 M2-T3 与校验器 M2-T4 共用）。
    /// 规则：大小写不敏感；拒绝纯数字（配置表禁止用数字代替枚举名，否则可读性与校验都会退化）；
    /// 拒绝未定义值；空/空白一律失败，由调用方决定"空列是否合法"。
    /// </summary>
    public static class ConfigTokens
    {
        /// <summary>解析单值枚举列（不适用于 flags 列，flags 走 KeywordTokens）。</summary>
        public static bool TryParseEnum<TEnum>(string? text, out TEnum value)
            where TEnum : struct, Enum
        {
            value = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string token = text.Trim();
            if (IsNumericToken(token))
            {
                return false;
            }

            if (!Enum.TryParse(token, ignoreCase: true, out TEnum parsed))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(TEnum), parsed))
            {
                return false;
            }

            value = parsed;
            return true;
        }

        /// <summary>是否是纯数字 token（含正负号），用于拒绝 "1" 这类写法。</summary>
        internal static bool IsNumericToken(string token)
        {
            if (token.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                bool isSign = (c == '-' || c == '+') && i == 0;
                if (!char.IsDigit(c) && !isSign)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
