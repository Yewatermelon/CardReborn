using System.Collections.Generic;
using Card.Core;

namespace Card.Application.Match
{
    /// <summary>
    /// 单座位开局请求：英雄 Key + 卡组卡牌 Key 列表（顺序=玩家构筑顺序，工厂负责洗牌）。
    /// 纯不可变输入；张数/Key 合法性由 <see cref="MatchFactory"/> 校验。
    /// </summary>
    public sealed class MatchSetupRequest
    {
        public MatchSetupRequest(string heroKey, IReadOnlyList<string> deckCardKeys)
        {
            HeroKey = Guard.NotNullOrWhiteSpace(heroKey, nameof(heroKey));
            DeckCardKeys = Guard.NotNull(deckCardKeys, nameof(deckCardKeys));
        }

        /// <summary>英雄配置 Key（指向 Heroes 表）。</summary>
        public string HeroKey { get; }

        /// <summary>卡组卡牌 Key 列表（可含重复，张数须等于 Rules.DeckSize）。</summary>
        public IReadOnlyList<string> DeckCardKeys { get; }
    }
}
