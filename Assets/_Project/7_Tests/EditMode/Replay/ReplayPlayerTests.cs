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
    /// M15-T1 交互式回放播放器测试（9 例）：覆盖 Play/Pause/Step/Seek/Speed +
    /// 边界（终局、空录制、被拒帧、SetSpeed 非正抛）。
    /// </summary>
    [TestFixture]
    public sealed class ReplayPlayerTests
    {
        private static readonly CardDatabase Db = MatchControllerFixtures.BuildDatabase();

        [Test]
        public void Play_ToEnd_StateFinished_CurrentFrameEqualsTotalFrames_SnapshotMatchesReplay()
        {
            MatchRecording recording = BuildRecording(seed: 42, stepCount: 70);
            var player = new ReplayPlayer(recording, Db);

            player.Play();
            while (player.Step()) { }

            Assert.That(player.State, Is.EqualTo(ReplayPlayerState.Finished));
            Assert.That(player.CurrentFrame, Is.EqualTo(player.TotalFrames));

            ReplayResult baseline = MatchReplayer.Replay(recording, Db);
            AssertStatesMatch(player.CurrentSnapshot, baseline.FinalState, "终局状态");
        }

        [Test]
        public void Pause_Then_Step_AdvancesOne_SnapshotMatchesBaseline()
        {
            MatchRecording recording = BuildRecording(seed: 7, stepCount: 10);
            var player = new ReplayPlayer(recording, Db);

            player.Play();
            player.Pause();
            Assert.That(player.State, Is.EqualTo(ReplayPlayerState.Paused));

            bool advanced = player.Step();
            Assert.That(advanced, Is.True);
            Assert.That(player.CurrentFrame, Is.EqualTo(1));

            MatchState baseline = BuildBaselineSnapshot(recording, 1);
            AssertStatesMatch(player.CurrentSnapshot, baseline, "帧 1 状态");
        }

        [Test]
        public void Step_AtEnd_ReturnsFalse_StateFinished()
        {
            MatchRecording recording = BuildRecording(seed: 5, stepCount: 70);
            var player = new ReplayPlayer(recording, Db);
            player.Play();
            while (player.Step()) { }
            Assert.That(player.State, Is.EqualTo(ReplayPlayerState.Finished));
            Assert.That(player.CurrentFrame, Is.EqualTo(player.TotalFrames));

            bool again = player.Step();
            Assert.That(again, Is.False);
            Assert.That(player.CurrentFrame, Is.EqualTo(player.TotalFrames));
        }

        [Test]
        public void Seek_ArbitraryFrame_MatchesBaseline()
        {
            MatchRecording recording = BuildRecording(seed: 11, stepCount: 10);
            var player = new ReplayPlayer(recording, Db);

            player.Seek(3);
            Assert.That(player.CurrentFrame, Is.EqualTo(3));
            Assert.That(player.State, Is.EqualTo(ReplayPlayerState.Paused));

            MatchState baseline = BuildBaselineSnapshot(recording, 3);
            AssertStatesMatch(player.CurrentSnapshot, baseline, "帧 3 状态");
        }

        [Test]
        public void Seek_Boundaries_ZeroAndTotal()
        {
            MatchRecording recording = BuildRecording(seed: 33, stepCount: 70);
            var player = new ReplayPlayer(recording, Db);

            player.Seek(0);
            Assert.That(player.CurrentFrame, Is.EqualTo(0));
            MatchState initial = BuildBaselineSnapshot(recording, 0);
            AssertStatesMatch(player.CurrentSnapshot, initial, "帧 0 状态");

            player.Seek(player.TotalFrames);
            Assert.That(player.State, Is.EqualTo(ReplayPlayerState.Finished));
            Assert.That(player.CurrentFrame, Is.EqualTo(player.TotalFrames));
        }

        [Test]
        public void EmptyRecording_TotalFramesZero_PlayImmediatelyFinished()
        {
            MatchRecording recording = BuildRecording(seed: 1, stepCount: 0);
            var player = new ReplayPlayer(recording, Db);

            Assert.That(player.TotalFrames, Is.EqualTo(0));
            player.Play();
            Assert.That(player.State, Is.EqualTo(ReplayPlayerState.Finished));

            bool advanced = player.Step();
            Assert.That(advanced, Is.False);
        }

        [Test]
        public void Step_ToRejectedFrame_StateUnchanged()
        {
            // 构造 1 条"错误玩家 EndTurn"录制：第 0 条由非行动方发起，RuleEngine 必拒
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(Db, deck, 88);
            int activeId = state.ActivePlayerId;
            int wrongId = 1 - activeId;
            string rejectedCmd = CommandSerializer.Serialize(new EndTurnCommand(wrongId));

            var recording = new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                88,
                new List<string> { rejectedCmd });

            // 独立前置验证：该命令确实被拒（与 ReplayPlayer 用同一条 RuleEngine 路径）
            MatchState baseline = MatchFactory.Create(
                Db, recording.Seat0, recording.Seat1, new SeededRandomProvider(88));
            var baselineController = new MatchController(baseline, Db);
            CommandResult rejectResult = baselineController.Submit(new EndTurnCommand(wrongId));
            Assert.That(rejectResult.IsInvalid, Is.True, "前置：错误玩家 EndTurn 应被拒");

            var player = new ReplayPlayer(recording, Db);
            bool advanced = player.Step();
            Assert.That(advanced, Is.True);
            Assert.That(player.CurrentFrame, Is.EqualTo(1));

            // 状态与"未提交任何命令"的初始状态一致（被拒零变更）
            MatchState initial = MatchFactory.Create(
                Db, recording.Seat0, recording.Seat1, new SeededRandomProvider(88));
            AssertStatesMatch(player.CurrentSnapshot, initial, "被拒帧应零变更");
        }

        [Test]
        public void SetSpeed_UpdatesSpeedProperty()
        {
            MatchRecording recording = BuildRecording(seed: 2, stepCount: 4);
            var player = new ReplayPlayer(recording, Db);

            Assert.That(player.Speed, Is.EqualTo(1f));

            player.SetSpeed(2f);
            Assert.That(player.Speed, Is.EqualTo(2f));

            player.SetSpeed(4f);
            Assert.That(player.Speed, Is.EqualTo(4f));
        }

        [Test]
        public void SetSpeed_NonPositive_Throws()
        {
            MatchRecording recording = BuildRecording(seed: 2, stepCount: 4);
            var player = new ReplayPlayer(recording, Db);

            try
            {
                player.SetSpeed(0f);
                Assert.Fail("应抛 ArgumentOutOfRangeException");
            }
            catch (System.ArgumentOutOfRangeException)
            {
                // 期望
            }
        }

        // ===== 助手 =====

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

        private static MatchState BuildBaselineSnapshot(MatchRecording recording, int frame)
        {
            MatchState state = MatchFactory.Create(
                Db, recording.Seat0, recording.Seat1,
                new SeededRandomProvider(recording.Seed));
            var controller = new MatchController(state, Db);
            for (int i = 0; i < frame; i++)
            {
                IGameCommand cmd = CommandSerializer.Deserialize(recording.Commands[i]);
                controller.Submit(cmd);
            }
            return state;
        }

        private static void AssertStatesMatch(MatchState actual, MatchState expected, string context)
        {
            Assert.That(actual.TurnNumber, Is.EqualTo(expected.TurnNumber), context + " TurnNumber");
            Assert.That(actual.ActivePlayerId, Is.EqualTo(expected.ActivePlayerId), context + " ActivePlayerId");
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase), context + " Phase");
            Assert.That(actual.IsFinished, Is.EqualTo(expected.IsFinished), context + " IsFinished");
            for (int i = 0; i < 2; i++)
            {
                PlayerState a = actual.GetPlayer(i);
                PlayerState b = expected.GetPlayer(i);
                Assert.That(a.Hero.Health, Is.EqualTo(b.Hero.Health), context + " seat" + i + " Health");
                Assert.That(a.Hero.Armor, Is.EqualTo(b.Hero.Armor), context + " seat" + i + " Armor");
                Assert.That(a.Mana.Current, Is.EqualTo(b.Mana.Current), context + " seat" + i + " Mana");
                Assert.That(a.FatigueCounter, Is.EqualTo(b.FatigueCounter), context + " seat" + i + " Fatigue");
                Assert.That(a.Deck.Count, Is.EqualTo(b.Deck.Count), context + " seat" + i + " Deck");
                Assert.That(a.Hand.Count, Is.EqualTo(b.Hand.Count), context + " seat" + i + " Hand");
                Assert.That(a.Board.Count, Is.EqualTo(b.Board.Count), context + " seat" + i + " Board");
            }
        }
    }
}
