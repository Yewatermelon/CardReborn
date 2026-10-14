using System.Collections.Generic;
using Card.Application.Match;
using Card.Application.Replay;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Replay
{
    /// <summary>
    /// M15-T1-B1：ReplayPlayer.AllEvents 暴露测试（4 例）。
    /// 验证回放每帧产生的事件可被表现层读取（操作日志接线的基础）。
    /// 纯 BCL，不引用 UnityEngine。
    /// </summary>
    [TestFixture]
    public sealed class ReplayPlayerEventsTests
    {
        private static readonly CardDatabase Db = MatchControllerFixtures.BuildDatabase();

        [Test]
        public void Step_ProducesEvents_AllEventsGrows_ContainsTurnEnded()
        {
            MatchRecording recording = BuildRecording(seed: 42, stepCount: 10);
            var player = new ReplayPlayer(recording, Db);

            Assert.That(player.AllEvents.Count, Is.EqualTo(0), "构造后无事件");

            player.Step();
            Assert.That(player.AllEvents.Count, Is.GreaterThan(0), "Step 后应产生事件");

            bool hasTurnEnded = false;
            for (int i = 0; i < player.AllEvents.Count; i++)
            {
                if (player.AllEvents[i] is TurnEndedEvent)
                {
                    hasTurnEnded = true;
                    break;
                }
            }

            Assert.That(hasTurnEnded, Is.True, "EndTurn 命令应产出 TurnEndedEvent");
        }

        [Test]
        public void Seek_Zero_AllEventsEmpty()
        {
            MatchRecording recording = BuildRecording(seed: 7, stepCount: 10);
            var player = new ReplayPlayer(recording, Db);

            player.Seek(0);
            Assert.That(player.AllEvents.Count, Is.EqualTo(0), "Seek(0) 后事件应为空");
        }

        [Test]
        public void Seek_ToEnd_AllEventsMatchesBaseline()
        {
            MatchRecording recording = BuildRecording(seed: 11, stepCount: 10);
            var player = new ReplayPlayer(recording, Db);

            player.Seek(player.TotalFrames);

            // 独立重放取基线事件数
            MatchState baseline = MatchFactory.Create(
                Db, recording.Seat0, recording.Seat1, new SeededRandomProvider(recording.Seed));
            var baselineController = new MatchController(baseline, Db);
            for (int i = 0; i < recording.Commands.Count; i++)
            {
                IGameCommand cmd = CommandSerializer.Deserialize(recording.Commands[i]);
                baselineController.Submit(cmd);
            }

            Assert.That(player.AllEvents.Count, Is.EqualTo(baselineController.Events.Count),
                "Seek 到末帧后事件数应与独立重放一致");
        }

        [Test]
        public void EmptyRecording_AllEventsEmpty()
        {
            MatchRecording recording = BuildRecording(seed: 1, stepCount: 0);
            var player = new ReplayPlayer(recording, Db);

            Assert.That(player.AllEvents.Count, Is.EqualTo(0), "空录制无事件");
        }

        // ===== 助手（与 ReplayPlayerTests 同口径，复制以避免改动既有测试文件） =====

        private static MatchRecording BuildRecording(int seed, int stepCount)
        {
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(Db, deck, seed);
            int firstSeat = state.ActivePlayerId;
            List<EndTurnCommand> script = MatchControllerFixtures.BuildEndTurnScript(firstSeat, stepCount);
            var commands = new List<string>(stepCount);
            for (int i = 0; i < script.Count; i++)
            {
                commands.Add(CommandSerializer.Serialize(script[i]));
            }
            return new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                seed,
                commands);
        }
    }
}
