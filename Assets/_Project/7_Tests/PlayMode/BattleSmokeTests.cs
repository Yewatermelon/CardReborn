using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Card.Application.Match;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Card.Tests.PlayMode
{
    /// <summary>
    /// M6-T2 端到端 PlayMode 冒烟测试：
    /// 经 BattleComposition 真实装配开局 → 驱动器出牌/攻击/结束回合 → 终局 → 断言事件流与胜负。
    /// 只测权威管线（Bootstrap→Controller→Settlers→Events→State），不经过 UI。
    /// </summary>
    [TestFixture]
    public sealed class BattleSmokeTests
    {
        private static string GeneratedConfigDir =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Config");

        /// <summary>完整对局：开局→出牌→攻击→结束回合→分胜负，可重复。</summary>
        [UnityTest]
        public IEnumerator Battle_EndToEnd_PlaysToCompletion()
        {
            // --- 开局 ---
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(GeneratedConfigDir);
            Assert.That(database, Is.Not.Null, "配置加载失败: " + string.Join("\n", errors));
            Assert.That(database!.EnabledCardCount, Is.GreaterThan(0), "启用卡数为 0");

            IReadOnlyList<string> deckKeys = BattleComposition.BuildDeckKeys(database);
            MatchController controller = BattleComposition.StartMatch(database, deckKeys, BattleComposition.DemoSeed);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.View.TurnNumber, Is.EqualTo(1), "首回合应为 1");
            Assert.That(controller.View.IsFinished, Is.False, "开局不应终局");

            // --- 驱动 ---
            DriveResult result = BattleSmokeDriver.Drive(controller, database, maxSteps: 1000);

            // 让 Unity 主循环跑一帧
            yield return null;

            // --- 断言 ---

            // AC-4: 终局
            Assert.That(controller.IsFinished, Is.True,
                "对局未在 " + result.StepsExecuted + " 步内终局");

            // AC-2: 至少打出过一张牌
            Assert.That(result.PlaysAccepted, Is.GreaterThan(0),
                "驱动器未成功打出任何牌（plays=0）");

            // AC-3: 至少执行过一次攻击
            Assert.That(result.AttacksAccepted, Is.GreaterThan(0),
                "驱动器未成功执行任何攻击（attacks=0）");

            // AC-5: 事件日志含 MatchEndEvent，终局至少一方 Hero Health ≤ 0
            var events = controller.Events;
            Assert.That(events, Is.Not.Empty, "事件日志为空");

            MatchEndedEvent? matchEnd = events.OfType<MatchEndedEvent>().FirstOrDefault();
            Assert.That(matchEnd, Is.Not.Null, "事件日志不含 MatchEndedEvent");
            Assert.That(matchEnd!.Result, Is.Not.EqualTo(MatchResult.Ongoing),
                "终局结果仍为 Ongoing");

            // 终局时至少一方英雄生命 ≤ 0 或疲劳致死
            IReadOnlyPlayerState p0 = controller.View.GetPlayer(0);
            IReadOnlyPlayerState p1 = controller.View.GetPlayer(1);
            bool someoneDead = p0.Hero.Health <= 0 || p1.Hero.Health <= 0;
            Assert.That(someoneDead || matchEnd.Reason.Contains("疲劳"),
                "终局时无一方英雄生命 ≤ 0，且原因非疲劳: "
                + "p0.HP=" + p0.Hero.Health + " p1.HP=" + p1.Hero.Health
                + " reason=" + matchEnd.Reason);

            // AC-6: 可重复——同种子再跑一遍，步数与事件数一致
            MatchController controller2 = BattleComposition.StartMatch(database, deckKeys, BattleComposition.DemoSeed);
            DriveResult result2 = BattleSmokeDriver.Drive(controller2, database, maxSteps: 1000);

            Assert.That(result2.StepsExecuted, Is.EqualTo(result.StepsExecuted),
                "同种子重复运行步数不一致（确定性被破坏）");
            Assert.That(result2.PlaysAccepted, Is.EqualTo(result.PlaysAccepted),
                "同种子重复运行出牌数不一致");
            Assert.That(result2.AttacksAccepted, Is.EqualTo(result.AttacksAccepted),
                "同种子重复运行攻击数不一致");
            Assert.That(controller2.Events.Count, Is.EqualTo(events.Count),
                "同种子重复运行事件数不一致");
        }

        /// <summary>配置缺失时不崩溃（AC-7 间接验证：BattleComposition 返回错误列表而非抛异常）。</summary>
        [UnityTest]
        public IEnumerator Battle_MissingConfig_ReturnsErrorsNotCrash()
        {
            string badDir = Path.Combine(System.IO.Path.GetTempPath(), "CardReborn_NoConfig_" + System.Guid.NewGuid().ToString("N"));
            (CardDatabase? db, var errors) = BattleComposition.LoadDatabase(badDir);

            Assert.That(db, Is.Null, "缺失配置不应返回数据库");
            Assert.That(errors, Is.Not.Empty, "缺失配置应返回错误列表");

            yield return null;
        }
    }
}
