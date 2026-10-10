using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Card.Tests.PlayMode
{
    /// <summary>
    /// M7-OBS-1：PVE 人机实盘 PlayMode 冒烟（AC-10）：
    /// 完整装配 AiTurnRunner + GreedyAiAgent(stepMode:true) → 人类回合由测试代发 EndTurn →
    /// AI 回合交给 AiTurnRunner.Update 每帧 StepOne 分帧驱动 → 终局断言。
    /// 不经过 UI 交互，测"分帧驱动器 + AI agent + 权威引擎"完整链路。
    /// 人类桩用本程序集内联 stub（EditMode 程序集的 ScriptedPlayerAgent 是 internal，跨程序集不可见）。
    /// </summary>
    [TestFixture]
    public sealed class PveAiTurnRunnerTests
    {
        private static string GeneratedConfigDir =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Config");

        [UnityTest]
        public IEnumerator PveAi_EndToEnd_StepsToCompletion()
        {
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(GeneratedConfigDir);
            Assert.That(database, Is.Not.Null, "配置加载失败: " + string.Join("\n", errors));

            IReadOnlyList<string> deckKeys = BattleComposition.BuildDeckKeys(database!);
            MatchController controller = BattleComposition.StartMatch(database!, deckKeys, BattleComposition.DemoSeed);

            var ai = new GreedyAiAgent(1, database!, stepMode: true);
            var agents = new IPlayerAgent[]
            {
                new IdleHumanAgent(0),  // 人类回合不决策，由测试代发 EndTurn
                ai,
            };
            var runner = new AgentMatchRunner(controller, controller.View, agents);

            var host = new GameObject("PveSmoke_Test");
            var aiRunner = host.AddComponent<AiTurnRunner>();
            aiRunner.Bind(runner, ai, controller, new BattleUi());

            runner.Start();

            Assert.That(controller.IsFinished, Is.False, "开局不应终局");
            Assert.That(controller.View.ActivePlayerId, Is.EqualTo(0), "座位 0 先手");

            // 驱动循环：人类回合由测试代发 EndTurn（OnSubmitAccepted→Pump→激活 AI）；
            // AI 回合完全交给 AiTurnRunner.Update（真实 MonoBehaviour 分帧路径）。
            int frames = 0;
            const int maxFrames = 5000;
            while (!controller.IsFinished && frames < maxFrames)
            {
                frames++;
                if (controller.View.ActivePlayerId == 0)
                {
                    runner.Submit(new EndTurnCommand(0));
                }

                yield return null;  // AiTurnRunner.Update 每帧 StepOne
            }

            UnityEngine.Object.DestroyImmediate(host);

            Assert.That(controller.IsFinished, Is.True,
                "PVE 人机对局未在 " + frames + " 帧内终局（max=" + maxFrames + "）");

            IReadOnlyPlayerState p0 = controller.View.GetPlayer(0);
            IReadOnlyPlayerState p1 = controller.View.GetPlayer(1);
            bool someoneDead = p0.Hero.Health <= 0 || p1.Hero.Health <= 0;
            MatchEndedEvent? matchEnd = controller.Events.OfType<MatchEndedEvent>().FirstOrDefault();
            Assert.That(matchEnd, Is.Not.Null, "事件日志不含 MatchEndedEvent");
            Assert.That(someoneDead || matchEnd!.Reason.Contains("疲劳"),
                "终局时无一方英雄生命 ≤ 0 且非疲劳：p0.HP=" + p0.Hero.Health
                + " p1.HP=" + p1.Hero.Health + " reason=" + matchEnd!.Reason);
        }

        /// <summary>人类座位桩：激活时不产生任何命令（真实玩家交互属实机冒烟）。</summary>
        private sealed class IdleHumanAgent : IPlayerAgent
        {
            public IdleHumanAgent(int playerId)
            {
                PlayerId = playerId;
            }

            public int PlayerId { get; }

            public void OnTurnActivated(IAgentContext context)
            {
            }

            public void OnTurnDeactivated()
            {
            }
        }
    }
}
