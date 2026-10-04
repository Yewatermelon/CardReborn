using System.Collections.Generic;
using Card.Domain.Config;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T6 测试用配置集合：按给定 key 造卡，其余表用最小合法数据。</summary>
    internal static class ConfigTestBundles
    {
        public static ConfigBundle WithCards(params string[] cardKeys)
        {
            List<CardDefinition> cards = new List<CardDefinition>(cardKeys.Length);
            for (int i = 0; i < cardKeys.Length; i++)
            {
                cards.Add(new CardDefinition
                {
                    Id = i + 1,
                    Key = cardKeys[i],
                    NameKey = cardKeys[i] + "_NAME",
                    DescKey = cardKeys[i] + "_DESC",
                    Cost = 1,
                    Type = CardType.Minion,
                    Rarity = CardRarity.Common,
                    Class = CardClass.Neutral,
                    Attack = 1,
                    Health = 1,
                    SetKey = "Core"
                });
            }

            return new ConfigBundle(
                cards,
                new[]
                {
                    new HeroDefinition
                    {
                        Id = 1,
                        Key = "HERO_MAGE",
                        NameKey = "HERO_001_NAME",
                        Health = 30,
                        HeroPowerKey = "HERO_POWER_FIREBALL",
                        Class = CardClass.Mage
                    }
                },
                new[]
                {
                    new HeroPowerDefinition
                    {
                        Id = 1,
                        Key = "HERO_POWER_FIREBALL",
                        Cost = 2,
                        TargetRule = TargetRule.Any,
                        Effects = new[] { "DamageEffect:1" }
                    }
                },
                new[]
                {
                    new RarityWeight { Rarity = CardRarity.Common, Weight = 70 },
                    new RarityWeight { Rarity = CardRarity.Rare, Weight = 22 },
                    new RarityWeight { Rarity = CardRarity.Epic, Weight = 6 },
                    new RarityWeight { Rarity = CardRarity.Legendary, Weight = 2 }
                },
                new GachaConfig { PackSize = 5, CoinCost = 100, PityCount = 10, PityRarity = CardRarity.Legendary },
                new RulesConfig { HeroHealth = 30, HandLimit = 10, BoardLimit = 7, ManaLimit = 10 });
        }
    }
}
