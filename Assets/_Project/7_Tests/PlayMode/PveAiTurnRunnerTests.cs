using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Bootstrap.Battle;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Card.Tests.PlayMode
{
    /// <summary>
    /// M7-OBS-1：PVE 人机实盘 PlayMode 冒烟（AC-6）：
    /// 完整装配 AiTurnRunner + GreedyAiAgent(stepMode:true) → 模拟若干帧 Update → 终局。
    /// 不经过 UI 交互，只测"分帧驱动器 + AI agent + 权威引擎"的完整链路。
    /// </summary>
    [TestFixture]
    public sealed class PveAiTurnRunnerTests
    {
        private const int Seed = 20261008;

        private static string GeneratedConfigDir =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Config");

        [UnityTest]
        public IEnumerator PveAi_EndToEnd_StepsToCompletion()
        {
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(GeneratedConfigDir);
            Assert.That(database, Is.Not.Null, "配置加载失败: " + string.Join("\n", errors));

            IReadOnlyList<string> deckKeys = BattleComposition.BuildDeckKeys(database!);
            MatchController controller = BattleComposition.StartMatch(database, deckKeys, Seed);

            var ai = new GreedyAiAgent(1, database, stepMode: true);
            var humans = new IPlayerAgent[]
            {
                new ScriptedPlayerAgent(0),  // 占位人类 agent，不决策
                ai,
            };
            var runner = new AgentMatchRunner(controller, controller.View, humans);

            var ui = new BattleUi();
            var host = new GameObject("PveSmoke_Test");
            var aiRunner = host.AddComponent<AiTurnRunner>();
            aiRunner.Bind(runner, ai, controller, ui);

            runner.Start();

            Assert.That(controller.IsFinished, Is.False, "开局不应终局");
            Assert.That(controller.View.ActivePlayerId, Is.EqualTo(0), "座位 0 先手");

            // 模拟人类发 EndTurn 一次（让 AI 开始行动）
            runner.Submit(new EndTurnCommand(0));
            runner.Pump();

            // 模拟 AI 回合分帧驱动：每帧 StepOne + 必要时 Pump
            int frames = 0;
            const int maxFrames = 5000;  // 安全上限

            while (!controller.IsFinished && frames < maxFrames)
            {
                frames++;
                bool aiActive = controller.View.ActivePlayerId == 1;

                if (aiActive)
                {
                    if (!ai.StepOne())
                    {
                        // AI 回合结束，发 EndTurn
                        runner.Submit(new EndTurnCommand(1));
                        // OnSubmitAccepted 会自动 Pump（因为绑了回调）
                    }
                }
                else
                {
                    // 人类回合（ScriptedPlayerAgent 不决策），直接发 EndTurn
                    runner.Submit(new EndTurnCommand(0));
                    // OnSubmitAccepted 自动 Pump 激活 AI
                }

                yield return null;
            }

            UnityEngine.Object.DestroyImmediate(host);

            Assert.That(controller.IsFinished, Is.True,
                "PVE 人机对局未在 " + frames + " 帧内终局（max=" + maxFrames + "）");

            // 终局基本断言
            IReadOnlyPlayerState p0 = controller.View.GetPlayer(0);
            IReadOnlyPlayerState p1 = controller.View.GetPlayer(1);
            bool someoneDead = p0.Hero.Health <= 0 || p1.Hero.Health <= 0;
            Assert.That(someoneDead, Is.True, "终局时无一方英雄生命 ≤ 0");

            var matchEnd = controller.Events.OfType<MatchEndedEvent>().FirstOrDefault();
            Assert.That(matchEnd, Is.Not.Null, "事件日志不含 MatchEndedEvent");
        }
    }
}
