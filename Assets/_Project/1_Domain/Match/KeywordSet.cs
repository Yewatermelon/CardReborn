using Card.Domain.Config;

namespace Card.Domain.Match
{
    /// <summary>
    /// 运行时可变关键词集合（M3-T3）：配置 <see cref="Keyword"/> 是不可变位标记，
    /// 运行时可能经增益/沉默增减，故独立为可变集合。所有操作按位运算，Add 幂等。
    /// </summary>
    public sealed class KeywordSet
    {
        public KeywordSet(Keyword keywords = Keyword.None)
        {
            Flags = keywords;
        }

        /// <summary>当前关键词位组合。</summary>
        public Keyword Flags { get; private set; }

        public bool IsEmpty => Flags == Keyword.None;

        /// <summary>是否包含给定关键词的全部位（<c>None</c> 按位运算恒为真）。</summary>
        public bool Has(Keyword keyword)
        {
            return (Flags & keyword) == keyword;
        }

        /// <summary>是否包含 <paramref name="keywords"/> 的每一位。</summary>
        public bool HasAll(Keyword keywords)
        {
            return (Flags & keywords) == keywords;
        }

        /// <summary>是否包含 <paramref name="keywords"/> 中的任意一位；<c>None</c> 为假。</summary>
        public bool HasAny(Keyword keywords)
        {
            return keywords != Keyword.None && (Flags & keywords) != Keyword.None;
        }

        /// <summary>加入一个或多个关键词（幂等）。</summary>
        public void Add(Keyword keyword)
        {
            Flags |= keyword;
        }

        /// <summary>移除一个或多个关键词（不含的位无副作用）。</summary>
        public void Remove(Keyword keyword)
        {
            Flags &= ~keyword;
        }

        /// <summary>按 <paramref name="enabled"/> 加入或移除关键词。</summary>
        public void Toggle(Keyword keyword, bool enabled)
        {
            if (enabled)
            {
                Add(keyword);
            }
            else
            {
                Remove(keyword);
            }
        }

        /// <summary>移除全部关键词。</summary>
        public void Clear()
        {
            Flags = Keyword.None;
        }
    }
}
