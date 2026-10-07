using System;
using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Infrastructure.Config;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 对战开局纯装配（M6-T1；无 UnityEngine 依赖，可被 EditMode 直接测试）：
    /// 配置目录 → <see cref="CardDatabase"/>；启用卡 → 牌组；固定双英雄 → <see cref="MatchController"/>。
    /// 英雄键沿用测试夹具口径（法师 vs 战士）。
    /// </summary>
    public static class BattleComposition
    {
        public const string MageHeroKey = "HERO_MAGE";
        public const string WarriorHeroKey = "HERO_WARRIOR";
        public const int DemoSeed = 20261007;

        /// <summary>从生成物目录加载配置并建库；失败时 db 为 null、errors 非空。</summary>
        public static (CardDatabase? Database, IReadOnlyList<string> Errors) LoadDatabase(string configDirectory)
        {
            Guard.NotNullOrWhiteSpace(configDirectory, nameof(configDirectory));

            ConfigLoadResult result = ConfigFileLoader.Load(configDirectory);
            if (!result.Succeeded || result.Bundle == null)
            {
                return (null, result.Errors);
            }

            return (new CardDatabase(result.Bundle), Array.Empty<string>());
        }

        /// <summary>
        /// 取前 <see cref="RulesConfig.DeckSize"/> 张启用卡作为演示牌组；
        /// 启用卡不足时循环补齐（保证数量精确等于 DeckSize）。
        /// </summary>
        public static IReadOnlyList<string> BuildDeckKeys(CardDatabase database)
        {
            Guard.NotNull(database, nameof(database));

            List<string> enabled = database.AllCards
                .Where(c => c.Enabled)
                .Select(c => c.Key)
                .ToList();
            if (enabled.Count == 0)
            {
                throw new InvalidOperationException("配置中没有启用卡牌，无法构建牌组。");
            }

            int deckSize = database.Rules.DeckSize;
            var deck = new List<string>(deckSize);
            for (int i = 0; i < deckSize; i++)
            {
                deck.Add(enabled[i % enabled.Count]);
            }

            return deck;
        }

        /// <summary>用固定双英雄与给定种子创建权威对局控制器（PVE 本地宿主）。</summary>
        public static MatchController StartMatch(CardDatabase database, IReadOnlyList<string> deckKeys, int seed = DemoSeed)
        {
            Guard.NotNull(database, nameof(database));
            Guard.NotNull(deckKeys, nameof(deckKeys));

            var seat0 = new MatchSetupRequest(MageHeroKey, deckKeys);
            var seat1 = new MatchSetupRequest(WarriorHeroKey, deckKeys);
            MatchState state = MatchFactory.Create(database, seat0, seat1, new SeededRandomProvider(seed));
            return new MatchController(state, database);
        }
    }
}
