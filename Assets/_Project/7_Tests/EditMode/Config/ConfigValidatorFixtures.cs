using Card.Core;
using Card.Domain.Config;

namespace Card.Tests.EditMode.Config
{
    /// <summary>M2-T4 测试夹具：合法基线 + 逐表替换，便于写出"只坏一处"的用例。</summary>
    internal static class ConfigValidatorFixtures
    {
        public const string CardHeader =
            "Id,Key,NameKey,DescKey,Cost,Type,Rarity,Class,Attack,Health,Keywords,TargetRule,Effects,SetKey,ArtKey,AudioKey,Enabled";

        public const string ValidCardRow =
            "1,NEUTRAL_PANGO,CARD_001_NAME,CARD_001_DESC,2,Minion,Common,Neutral,2,3,Taunt,None,,Core,art_card_001,sfx_play_001,TRUE";

        public const string HeroHeader = "Id,Key,NameKey,Health,HeroPowerKey,Class";
        public const string ValidHeroRow = "1,HERO_MAGE,HERO_001_NAME,30,HERO_POWER_FIREBALL,Mage";
        public const string PowerHeader = "Id,Key,Cost,TargetRule,Effects";
        public const string ValidPowerRow = "1,HERO_POWER_FIREBALL,2,Any,DamageEffect:1";
        public const string WeightHeader = "Rarity,Weight,MinPerPack";
        public const string ValidWeightsBody = "Common,70,0\nRare,22,1\nEpic,6,0\nLegendary,2,0";
        public const string ValidGachaCsv = "PackSize,CoinCost,PityCount,PityRarity\n5,100,10,Legendary";
        public const string ValidRulesCsv = "HeroHealth,HandLimit,BoardLimit,ManaLimit\n30,10,7,10";

        public static CsvTable Cards(string row)
        {
            return CsvTable.Parse(CardHeader + "\n" + row + "\n");
        }

        public static CsvTable CardsWith(string firstRow, string secondRow)
        {
            return CsvTable.Parse(CardHeader + "\n" + firstRow + "\n" + secondRow + "\n");
        }

        public static CsvTable Heroes(string row)
        {
            return CsvTable.Parse(HeroHeader + "\n" + row + "\n");
        }

        public static CsvTable Powers(string row)
        {
            return CsvTable.Parse(PowerHeader + "\n" + row + "\n");
        }

        public static CsvTable Weights(string body)
        {
            return CsvTable.Parse(WeightHeader + "\n" + body + "\n");
        }

        public static CsvTable Rules(string row)
        {
            return CsvTable.Parse("HeroHealth,HandLimit,BoardLimit,ManaLimit\n" + row + "\n");
        }

        public static CsvTable Gacha(string row)
        {
            return CsvTable.Parse("PackSize,CoinCost,PityCount,PityRarity\n" + row + "\n");
        }

        public static ConfigSourceSet Build(
            CsvTable? cards = null,
            CsvTable? heroes = null,
            CsvTable? powers = null,
            CsvTable? weights = null,
            CsvTable? gacha = null,
            CsvTable? rules = null)
        {
            return new ConfigSourceSet(
                cards ?? Cards(ValidCardRow),
                heroes ?? Heroes(ValidHeroRow),
                powers ?? Powers(ValidPowerRow),
                weights ?? Weights(ValidWeightsBody),
                gacha ?? Gacha("5,100,10,Legendary"),
                rules ?? Rules("30,10,7,10"));
        }
    }
}
