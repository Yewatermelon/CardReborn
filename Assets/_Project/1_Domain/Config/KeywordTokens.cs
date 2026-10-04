using System;
using System.Text;

namespace Card.Domain.Config
{
    /// <summary>
    /// 关键词列的文本契约：Taunt|Charge 与 Keyword 互转。
    /// 解析规则：null / 空 / 纯空白 = 无关键词（合法）；大小写不敏感；容忍空白与重复；
    /// 未知 token 立即失败并报出名字（由 M2-T4 带上行号报错）。
    /// </summary>
    public static class KeywordTokens
    {
        /// <summary>分隔符（与模板一致）。</summary>
        public const char Separator = '|';

        private static readonly Keyword[] AllKeywords =
        {
            Keyword.Taunt,
            Keyword.Charge,
            Keyword.DivineShield,
            Keyword.Battlecry,
            Keyword.Deathrattle,
            Keyword.Windfury,
            Keyword.Stealth,
            Keyword.Poisonous,
            Keyword.Frozen,
            Keyword.Rush,
            Keyword.SpellPower,
            Keyword.Lifesteal
        };

        /// <summary>按固定顺序格式化（生成物稳定、便于 diff）；空集输出空串。</summary>
        public static string Format(Keyword keywords)
        {
            if (keywords == Keyword.None)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < AllKeywords.Length; i++)
            {
                Keyword value = AllKeywords[i];
                if ((keywords & value) != value)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(Separator);
                }

                builder.Append(value.ToString());
            }

            return builder.ToString();
        }

        /// <summary>解析关键词列；失败时 failedToken 给出第一个无法识别的 token。</summary>
        public static bool TryParse(string? text, out Keyword keywords, out string? failedToken)
        {
            keywords = Keyword.None;
            failedToken = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string[] tokens = text.Split(Separator);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                if (ConfigTokens.IsNumericToken(token) ||
                    !Enum.TryParse(token, ignoreCase: true, out Keyword parsed) ||
                    parsed == Keyword.None ||
                    !Enum.IsDefined(typeof(Keyword), parsed))
                {
                    keywords = Keyword.None;
                    failedToken = token;
                    return false;
                }

                keywords |= parsed;
            }

            return true;
        }
    }
}
