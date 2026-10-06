using System.Collections.Generic;
using System.Linq;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Config;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M4-T2 共享夹具：真实源表构造数据库/卡组/对局（与 MatchFactoryTests 同口径），
    /// 以及"只结束回合"疲劳脚本的构造与执行。
    /// </summary>
    internal static class MatchControllerFixtures
    {
        public const string MageHero = "HERO_MAGE";
        public const string WarriorHero = "HERO_WARRIOR";

        /// <summary>脚本安全上限（实际终局在第 67 步）。</summary>
        public const int MaxScriptSteps = 80;

        public static CardDatabase BuildDatabase()
        {
            ConfigSourceSet sources = new ConfigSourceSet(
                ConfigTemplates.Load(ConfigTemplates.CardsFile),
                ConfigTemplates.Load(ConfigTemplates.HeroesFile),
                ConfigTemplates.Load(ConfigTemplates.HeroPowersFile),
                ConfigTemplates.Load(ConfigTemplates.RarityWeightsFile),
                ConfigTemplates.Load(ConfigTemplates.GachaConfigFile),
                ConfigTemplates.Load(ConfigTemplates.RulesFile));
            ConfigValidationReport report = ConfigValidator.Validate(sources);
            Assert.That(report.HasErrors, Is.False, report.ToText());
            return new CardDatabase(report.Result!);
        }

        public static IReadOnlyList<string> LoadEnabledDeckKeys()
        {
            CsvTable cards = ConfigTemplates.Load(ConfigTemplates.CardsFile);
            List<string> keys = new List<string>();
            for (int row = 0; row < cards.RowCount && keys.Count < 30; row++)
            {
                if (cards.GetCell(row, "Enabled") == "TRUE")
                {
                    keys.Add(cards.GetCell(row, "Key"));
                }
            }

            return keys;
        }

        public static MatchState NewMatch(CardDatabase database, IReadOnlyList<string> deck, int seed)
        {
            return MatchFactory.Create(
                database,
                new MatchSetupRequest(MageHero, deck),
                new MatchSetupRequest(WarriorHero, deck),
                new SeededRandomProvider(seed));
        }

        /// <summary>
        /// 生成交替座位的 EndTurn 脚本：第 i 条的发起者 = (firstSeat + i) % 2
        /// （回合 t 的行动方为 (firstSeat + t - 1) % 2，FIFO 处理顺序与之一致）。
        /// </summary>
        public static List<EndTurnCommand> BuildEndTurnScript(int firstSeat, int count)
        {
            return Enumerable.Range(0, count)
                .Select(i => new EndTurnCommand((firstSeat + i) % 2))
                .ToList();
        }

        /// <summary>
        /// 逐条提交脚本直至终局或达到上限；返回已处理条数。
        /// 终局前每一步都必须被接受（脚本本身全合法）。
        /// </summary>
        public static int SubmitUntilFinished(MatchController controller, int firstSeat)
        {
            int seat = firstSeat;
            int steps = 0;
            while (steps < MaxScriptSteps && !controller.IsFinished)
            {
                CommandResult result = controller.Submit(new EndTurnCommand(seat));
                Assert.That(result.IsValid, Is.True,
                    "疲劳脚本第 " + (steps + 1) + " 步意外被拒：" + result.Error);
                seat = 1 - seat;
                steps++;
            }

            Assert.That(controller.IsFinished, Is.True, "脚本上限内未终局，请检查疲劳推演。");
            return steps;
        }
    }
}
