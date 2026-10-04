using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Config
{
    /// <summary>
    /// M2-T7：首版内容目标（Docs/01 第 8 节）是否达成。
    /// 直接用源表 → 契约 → 校验器 建库，因此内容不达标会在测试里失败，而不是等到玩起来才发现。
    /// </summary>
    public sealed class ConfigContentTests
    {
        [Test]
        public void Cards_MeetMinimumContentTargets()
        {
            CardDatabase database = BuildDatabase();

            Assert.That(database.CardCount, Is.GreaterThanOrEqualTo(30), "首版至少 30 张卡");
            Assert.That(database.EnabledCardCount, Is.GreaterThanOrEqualTo(30), "启用中的卡也要 ≥ 30");
        }

        [Test]
        public void Cards_HaveEnoughMinionsAndSpells()
        {
            CardDatabase database = BuildDatabase();
            int minions = 0;
            int spells = 0;

            IReadOnlyList<CardDefinition> enabled = database.FilterCards();
            for (int i = 0; i < enabled.Count; i++)
            {
                if (enabled[i].Type == CardType.Minion)
                {
                    minions++;
                }
                else if (enabled[i].Type == CardType.Spell)
                {
                    spells++;
                }
            }

            Assert.That(minions, Is.GreaterThanOrEqualTo(20), "随从 ≥ 20");
            Assert.That(spells, Is.GreaterThanOrEqualTo(10), "法术 ≥ 10");
        }

        [Test]
        public void Cards_CoverEveryRarity()
        {
            CardDatabase database = BuildDatabase();
            IReadOnlyList<CardDefinition> enabled = database.FilterCards();
            bool[] seen = new bool[4];

            for (int i = 0; i < enabled.Count; i++)
            {
                seen[(int)enabled[i].Rarity] = true;
            }

            for (int i = 0; i < seen.Length; i++)
            {
                Assert.That(seen[i], Is.True, "缺少稀有度：" + (CardRarity)i);
            }
        }

        [Test]
        public void Cards_CoverTheRequiredKeywords()
        {
            CardDatabase database = BuildDatabase();
            IReadOnlyList<CardDefinition> enabled = database.FilterCards();
            Keyword[] required =
            {
                Keyword.Taunt, Keyword.Charge, Keyword.DivineShield, Keyword.Battlecry, Keyword.Deathrattle
            };

            for (int i = 0; i < required.Length; i++)
            {
                bool found = false;
                for (int c = 0; c < enabled.Count; c++)
                {
                    if ((enabled[c].Keywords & required[i]) == required[i])
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True, "首版必须用到关键词：" + required[i]);
            }
        }

        [Test]
        public void Cards_UseAtLeastEightDistinctEffects()
        {
            CardDatabase database = BuildDatabase();
            IReadOnlyList<CardDefinition> enabled = database.FilterCards();
            HashSet<string> effectNames = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < enabled.Count; i++)
            {
                IReadOnlyList<string> effects = enabled[i].Effects;
                for (int e = 0; e < effects.Count; e++)
                {
                    string text = effects[e];
                    int separator = text.IndexOf(':');
                    effectNames.Add(separator < 0 ? text : text.Substring(0, separator));
                }
            }

            Assert.That(effectNames.Count, Is.GreaterThanOrEqualTo(8), "首版至少用 8 种效果组件");
        }

        [Test]
        public void Cards_ClassPoolsSupportBothHeroes()
        {
            CardDatabase database = BuildDatabase();
            string[] heroKeys = HeroKeys(database);

            for (int i = 0; i < heroKeys.Length; i++)
            {
                HeroDefinition hero = database.RequireHero(heroKeys[i]);
                Assert.That(database.RequireHeroPower(hero.HeroPowerKey).Key, Is.EqualTo(hero.HeroPowerKey));
            }

            Assert.That(database.FilterCards(cardClass: CardClass.Mage).Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(database.FilterCards(cardClass: CardClass.Warrior).Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void DisabledCards_StayOutOfTheEnabledPool()
        {
            CardDatabase database = BuildDatabase();

            Assert.That(database.CardCount, Is.GreaterThan(database.EnabledCardCount), "示例数据含废弃卡");
            Assert.That(database.TryGetCard("NEUTRAL_OLD_TEST", out _), Is.True, "废弃卡仍可按键查到（供回放/兼容）");

            IReadOnlyList<CardDefinition> enabled = database.FilterCards();
            for (int i = 0; i < enabled.Count; i++)
            {
                Assert.That(enabled[i].Enabled, Is.True);
            }
        }

        [Test]
        public void HeroPowers_AreWiredToHeroes()
        {
            CardDatabase database = BuildDatabase();

            Assert.That(database.HeroCount, Is.GreaterThanOrEqualTo(2), "首版 2 个英雄");
            string[] keys = HeroKeys(database);
            for (int i = 0; i < keys.Length; i++)
            {
                HeroDefinition hero = database.RequireHero(keys[i]);
                Assert.That(hero.HeroPowerKey, Is.Not.Empty);
                Assert.That(database.RequireHeroPower(hero.HeroPowerKey).Cost, Is.GreaterThan(0));
            }
        }

        private static string[] HeroKeys(CardDatabase database)
        {
            CsvTable heroes = ConfigTemplates.Load(ConfigTemplates.HeroesFile);
            string[] keys = new string[heroes.RowCount];
            for (int i = 0; i < heroes.RowCount; i++)
            {
                keys[i] = heroes.GetCell(i, "Key");
            }

            return keys;
        }

        private static CardDatabase BuildDatabase()
        {
            CsvTable cards = ConfigTemplates.Load(ConfigTemplates.CardsFile);
            List<CardDefinition> parsed = new List<CardDefinition>(cards.RowCount);
            for (int row = 0; row < cards.RowCount; row++)
            {
                parsed.Add(ConfigRowParser.ParseCard(cards, row, ConfigTemplates.CardsFile));
            }

            ConfigSourceSet sources = new ConfigSourceSet(
                cards,
                ConfigTemplates.Load(ConfigTemplates.HeroesFile),
                ConfigTemplates.Load(ConfigTemplates.HeroPowersFile),
                ConfigTemplates.Load(ConfigTemplates.RarityWeightsFile),
                ConfigTemplates.Load(ConfigTemplates.GachaConfigFile),
                ConfigTemplates.Load(ConfigTemplates.RulesFile));
            ConfigValidationReport report = ConfigValidator.Validate(sources);

            Assert.That(report.HasErrors, Is.False, report.ToText());
            return new CardDatabase(report.Result!);
        }
    }
}
