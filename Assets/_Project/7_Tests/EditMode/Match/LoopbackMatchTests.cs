using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// 进程内回环模拟测试（M4-T10）：权威服务器 ↔ 只读客户端视图经内存通道同步。
    /// </summary>
    [TestFixture]
    public class LoopbackMatchTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        [Test]
        public void LoopbackLink_FIFO_Bidirectional()
        {
            LoopbackLink.CreatePair(out LoopbackEndpoint a, out LoopbackEndpoint b);

            a.Send("msg-a1");
            a.Send("msg-a2");
            Assert.That(b.Pending, Is.EqualTo(2));
            Assert.That(a.Pending, Is.EqualTo(0));

            b.Send("msg-b1");
            Assert.That(a.Pending, Is.EqualTo(1));

            Assert.That(b.TryReceive(out string? m1), Is.True);
            Assert.That(m1, Is.EqualTo("msg-a1"));
            Assert.That(b.TryReceive(out string? m2), Is.True);
            Assert.That(m2, Is.EqualTo("msg-a2"));
            Assert.That(b.TryReceive(out _), Is.False);

            Assert.That(a.TryReceive(out string? m3), Is.True);
            Assert.That(m3, Is.EqualTo("msg-b1"));
            Assert.That(a.TryReceive(out _), Is.False);
        }

        [Test]
        public void HelloSnapshot_ViewMatchesAuthority()
        {
            MatchController controller = NewController();
            LoopbackServer server = new LoopbackServer(controller);
            LoopbackClient client = new LoopbackClient();
            Link(server, client);

            client.RequestSnapshot();
            server.Pump();
            client.Pump();

            Assert.That(client.IsConnected, Is.True);
            Assert.That(client.Version, Is.EqualTo(0));
            Assert.That(client.View, Is.Not.Null);
            Assert.That(
                MatchStateSerializer.Serialize(client.View!),
                Is.EqualTo(MatchStateSerializer.Serialize(controller.State)));
        }

        [Test]
        public void EndTurn_Accepted_BroadcastToBothClients()
        {
            MatchController controller = NewController();
            LoopbackServer server = new LoopbackServer(controller);
            (LoopbackClient a, LoopbackClient b) = ConnectBoth(server);

            int firstSeat = controller.State.ActivePlayerId;
            LoopbackClient active = firstSeat == 0 ? a : b;

            active.SubmitCommand(new EndTurnCommand(firstSeat));
            server.Pump();
            a.Pump();
            b.Pump();

            Assert.That(server.Version, Is.EqualTo(1));
            Assert.That(a.LastAccepted, Is.True);
            Assert.That(a.LastError, Is.EqualTo(CommandError.None));
            Assert.That(b.LastAccepted, Is.True);
            Assert.That(Serialize(a.View!), Is.EqualTo(Serialize(controller.State)));
            Assert.That(Serialize(b.View!), Is.EqualTo(Serialize(controller.State)));
        }

        [Test]
        public void NonActivePlayer_CommandRejected_ViewAndVersionUnchanged()
        {
            MatchController controller = NewController();
            LoopbackServer server = new LoopbackServer(controller);
            (LoopbackClient a, LoopbackClient b) = ConnectBoth(server);

            int nonActive = 1 - controller.State.ActivePlayerId;
            LoopbackClient offender = nonActive == 0 ? a : b;
            string beforeA = Serialize(a.View!);
            string beforeB = Serialize(b.View!);

            offender.SubmitCommand(new EndTurnCommand(nonActive));
            server.Pump();
            a.Pump();
            b.Pump();

            Assert.That(server.Version, Is.EqualTo(0));
            Assert.That(offender.LastAccepted, Is.False);
            Assert.That(offender.LastError, Is.EqualTo(CommandError.NotYourTurn));
            Assert.That(Serialize(a.View!), Is.EqualTo(beforeA));
            Assert.That(Serialize(b.View!), Is.EqualTo(beforeB));
        }

        [Test]
        public void SubmitCommand_WhenNotConnected_Throws()
        {
            LoopbackClient client = new LoopbackClient();
            Assert.Throws<InvalidOperationException>(
                () => client.SubmitCommand(new EndTurnCommand(0)));
        }

        [Test]
        public void DualClient_FatigueScript_Finishes_ConsistentState()
        {
            MatchController controller = NewController();
            LoopbackServer server = new LoopbackServer(controller);
            (LoopbackClient c0, LoopbackClient c1) = ConnectBoth(server);
            int initialActive = controller.State.ActivePlayerId;
            int acceptedCount = 0;

            for (int step = 0; step < MatchControllerFixtures.MaxScriptSteps && !controller.IsFinished; step++)
            {
                int seat = controller.State.ActivePlayerId;
                LoopbackClient actor = seat == 0 ? c0 : c1;
                actor.SubmitCommand(new EndTurnCommand(seat));
                server.Pump();
                c0.Pump();
                c1.Pump();

                Assert.That(c0.LastAccepted, Is.True, "第 " + (step + 1) + " 步被 c0 拒");
                Assert.That(c1.LastAccepted, Is.True, "第 " + (step + 1) + " 步被 c1 拒");
                acceptedCount++;

                string auth = Serialize(controller.State);
                Assert.That(Serialize(c0.View!), Is.EqualTo(auth), "c0 第 " + (step + 1) + " 步不一致");
                Assert.That(Serialize(c1.View!), Is.EqualTo(auth), "c1 第 " + (step + 1) + " 步不一致");
            }

            Assert.That(controller.IsFinished, Is.True);
            Assert.That(controller.LastOutcome, Is.Not.Null);
            Assert.That(controller.LastOutcome!.WinnerId, Is.EqualTo(initialActive));
            Assert.That(server.Version, Is.EqualTo(acceptedCount));
        }

        [Test]
        public void Version_EqualsAcceptedCommandCount()
        {
            MatchController controller = NewController();
            LoopbackServer server = new LoopbackServer(controller);
            LoopbackClient client = new LoopbackClient();
            Link(server, client);
            client.RequestSnapshot();
            server.Pump();
            client.Pump();

            Assert.That(server.Version, Is.EqualTo(0));
            Assert.That(client.Version, Is.EqualTo(0));

            int seat = controller.State.ActivePlayerId;
            client.SubmitCommand(new EndTurnCommand(seat));
            server.Pump();
            client.Pump();
            Assert.That(server.Version, Is.EqualTo(1));
            Assert.That(client.Version, Is.EqualTo(1));

            // 拒绝命令不改变版本（此时行动方已换人，原行动方再发命令会被拒）
            int rejectedSeat = seat;
            client.SubmitCommand(new EndTurnCommand(rejectedSeat));
            server.Pump();
            client.Pump();
            Assert.That(client.LastAccepted, Is.False);
            Assert.That(client.LastError, Is.EqualTo(CommandError.NotYourTurn));
            Assert.That(server.Version, Is.EqualTo(1));
            Assert.That(client.Version, Is.EqualTo(1));
        }

        // -----------------------------------------------------------------
        // 辅助
        // -----------------------------------------------------------------

        private MatchController NewController()
        {
            return new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42),
                _database);
        }

        private static string Serialize(MatchState state) => MatchStateSerializer.Serialize(state);

        private static void Link(LoopbackServer server, LoopbackClient client)
        {
            LoopbackLink.CreatePair(out LoopbackEndpoint s, out LoopbackEndpoint c);
            server.Attach(s);
            client.Attach(c);
        }

        private (LoopbackClient, LoopbackClient) ConnectBoth(LoopbackServer server)
        {
            LoopbackClient c0 = new LoopbackClient();
            LoopbackClient c1 = new LoopbackClient();
            LoopbackLink.CreatePair(out LoopbackEndpoint s0, out LoopbackEndpoint c0e);
            LoopbackLink.CreatePair(out LoopbackEndpoint s1, out LoopbackEndpoint c1e);
            server.Attach(s0);
            server.Attach(s1);
            c0.Attach(c0e);
            c1.Attach(c1e);

            c0.RequestSnapshot();
            server.Pump();
            c0.Pump();

            c1.RequestSnapshot();
            server.Pump();
            c1.Pump();

            return (c0, c1);
        }
    }
}
