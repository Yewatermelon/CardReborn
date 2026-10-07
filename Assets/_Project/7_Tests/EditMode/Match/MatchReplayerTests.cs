using System.Collections.Generic;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>M4-T9 对局录制与重放测试：真实配置 + 疲劳脚本 + 同 seed 重放。</summary>
    [TestFixture]
    public sealed class MatchReplayerTests
    {
        private static readonly CardDatabase Db = MatchControllerFixtures.BuildDatabase();

        [Test]
        public void Replay_SameSeedAndCommands_ProducesSameFinalState()
        {
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            const int seed = 42;

            MatchState original = MatchControllerFixtures.NewMatch(Db, deck, seed);
            var controller = new MatchController(original, Db);
            int firstSeat = original.ActivePlayerId;

            // 原局：走 10 步结束回合脚本
            List<EndTurnCommand> script = MatchControllerFixtures.BuildEndTurnScript(firstSeat, 10);
            foreach (EndTurnCommand cmd in script)
            {
                controller.Submit(cmd);
            }

            // 录制
            var cmdStrings = new List<string>();
            foreach (EndTurnCommand cmd in script)
            {
                cmdStrings.Add(CommandSerializer.Serialize(cmd));
            }
            var recording = new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                seed,
                cmdStrings);

            // 重放
            ReplayResult result = MatchReplayer.Replay(recording, Db);

            // 断言：终局状态关键字段一致
            Assert.That(result.FinalState.TurnNumber, Is.EqualTo(original.TurnNumber));
            Assert.That(result.FinalState.ActivePlayerId, Is.EqualTo(original.ActivePlayerId));
            Assert.That(result.FinalState.Phase, Is.EqualTo(original.Phase));
            for (int i = 0; i < 2; i++)
            {
                PlayerState a = result.FinalState.GetPlayer(i);
                PlayerState b = original.GetPlayer(i);
                Assert.That(a.Hero.Health, Is.EqualTo(b.Hero.Health), "座位 " + i + " 英雄血量不一致");
                Assert.That(a.Mana.Current, Is.EqualTo(b.Mana.Current), "座位 " + i + " 法力不一致");
                Assert.That(a.FatigueCounter, Is.EqualTo(b.FatigueCounter), "座位 " + i + " 疲劳计数不一致");
                Assert.That(a.Deck.Count, Is.EqualTo(b.Deck.Count), "座位 " + i + " 牌库数不一致");
                Assert.That(a.Hand.Count, Is.EqualTo(b.Hand.Count), "座位 " + i + " 手牌数不一致");
            }
        }

        [Test]
        public void Replay_SameSeedAndCommands_ProducesSameEventSequence()
        {
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            const int seed = 123;

            MatchState original = MatchControllerFixtures.NewMatch(Db, deck, seed);
            var controller = new MatchController(original, Db);
            int firstSeat = original.ActivePlayerId;

            List<EndTurnCommand> script = MatchControllerFixtures.BuildEndTurnScript(firstSeat, 6);
            foreach (EndTurnCommand cmd in script)
            {
                controller.Submit(cmd);
            }

            var cmdStrings = new List<string>();
            foreach (EndTurnCommand cmd in script)
            {
                cmdStrings.Add(CommandSerializer.Serialize(cmd));
            }
            var recording = new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                seed,
                cmdStrings);

            ReplayResult result = MatchReplayer.Replay(recording, Db);

            // 断言：事件序列完全一致（类型与数量）
            Assert.That(result.Events.Count, Is.EqualTo(controller.Events.Count));
            for (int i = 0; i < result.Events.Count; i++)
            {
                Assert.That(
                    result.Events[i].GetType(),
                    Is.EqualTo(controller.Events[i].GetType()),
                    "第 " + i + " 个事件类型不一致");
            }
        }

        [Test]
        public void Replay_SameSeedAndCommands_ProducesSameHistory()
        {
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            const int seed = 99;

            MatchState original = MatchControllerFixtures.NewMatch(Db, deck, seed);
            var controller = new MatchController(original, Db);
            int firstSeat = original.ActivePlayerId;

            List<EndTurnCommand> script = MatchControllerFixtures.BuildEndTurnScript(firstSeat, 4);
            foreach (EndTurnCommand cmd in script)
            {
                controller.Submit(cmd);
            }

            var cmdStrings = new List<string>();
            foreach (EndTurnCommand cmd in script)
            {
                cmdStrings.Add(CommandSerializer.Serialize(cmd));
            }
            var recording = new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                seed,
                cmdStrings);

            ReplayResult result = MatchReplayer.Replay(recording, Db);

            // 断言：History 命令类型序列与接受标记一致
            Assert.That(result.History.Count, Is.EqualTo(controller.History.Count));
            for (int i = 0; i < result.History.Count; i++)
            {
                Assert.That(
                    result.History[i].CommandType,
                    Is.EqualTo(controller.History[i].CommandType),
                    "第 " + i + " 条台账命令类型不一致");
                Assert.That(
                    result.History[i].Accepted,
                    Is.EqualTo(controller.History[i].Accepted),
                    "第 " + i + " 条台账接受标记不一致");
            }
        }

        [Test]
        public void MatchRecording_ToJson_FromJson_RoundTrips()
        {
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            var recording = new MatchRecording(
                new MatchSetupRequest(MatchControllerFixtures.MageHero, deck),
                new MatchSetupRequest(MatchControllerFixtures.WarriorHero, deck),
                777,
                new List<string> { "{ \"type\": \"EndTurn\", \"playerId\": 0 }" });

            string json = recording.ToJson();
            MatchRecording back = MatchRecording.FromJson(json);

            Assert.That(back.Seed, Is.EqualTo(777));
            Assert.That(back.Seat0.HeroKey, Is.EqualTo(MatchControllerFixtures.MageHero));
            Assert.That(back.Seat1.HeroKey, Is.EqualTo(MatchControllerFixtures.WarriorHero));
            Assert.That(back.Seat0.DeckCardKeys.Count, Is.EqualTo(deck.Count));
            Assert.That(back.Commands.Count, Is.EqualTo(1));
            Assert.That(back.Commands[0], Is.EqualTo(recording.Commands[0]));
        }

        [Test]
        public void MatchRecording_FromJson_MissingMember_Throws()
        {
            const string json = "{ \"seed\": 1 }";

            try
            {
                MatchRecording.FromJson(json);
                Assert.Fail("应抛 ArgumentException");
            }
            catch (System.ArgumentException)
            {
            }
        }
    }
}
