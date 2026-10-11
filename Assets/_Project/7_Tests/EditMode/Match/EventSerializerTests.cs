using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// EventSerializer 往返测试（M11-T6）：12 种事件类型逐个序列化→反序列化→字段断言。
    /// </summary>
    [TestFixture]
    public class EventSerializerTests
    {
        // -----------------------------------------------------------------
        // 逐类型往返
        // -----------------------------------------------------------------

        [Test]
        public void PhaseChanged_Roundtrip()
        {
            var e = new PhaseChangedEvent(TurnPhase.Draw, TurnPhase.Main);
            e.Sequence = 3;
            var json = EventSerializer.Serialize(e);
            var back = EventSerializer.Deserialize(json);
            Assert.That(back, Is.TypeOf<PhaseChangedEvent>());
            var pc = (PhaseChangedEvent)back;
            Assert.That(pc.Sequence, Is.EqualTo(3));
            Assert.That(pc.From, Is.EqualTo(TurnPhase.Draw));
            Assert.That(pc.To, Is.EqualTo(TurnPhase.Main));
        }

        [Test]
        public void TurnStarted_Roundtrip()
        {
            var e = new TurnStartedEvent(5, 1);
            e.Sequence = 10;
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            Assert.That(back, Is.TypeOf<TurnStartedEvent>());
            var ts = (TurnStartedEvent)back;
            Assert.That(ts.Sequence, Is.EqualTo(10));
            Assert.That(ts.TurnNumber, Is.EqualTo(5));
            Assert.That(ts.ActiveSeat, Is.EqualTo(1));
        }

        [Test]
        public void TurnEnded_Roundtrip()
        {
            var e = new TurnEndedEvent(5, 1);
            e.Sequence = 12;
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var te = (TurnEndedEvent)back;
            Assert.That(te.TurnNumber, Is.EqualTo(5));
            Assert.That(te.ActiveSeat, Is.EqualTo(1));
            Assert.That(te.Sequence, Is.EqualTo(12));
        }

        [Test]
        public void CardDrawn_Roundtrip()
        {
            var e = new CardDrawnEvent(0, 42);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var cd = (CardDrawnEvent)back;
            Assert.That(cd.Seat, Is.EqualTo(0));
            Assert.That(cd.CardInstanceId, Is.EqualTo(42));
        }

        [Test]
        public void CardBurned_Roundtrip()
        {
            var e = new CardBurnedEvent(1, 99);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var cb = (CardBurnedEvent)back;
            Assert.That(cb.Seat, Is.EqualTo(1));
            Assert.That(cb.CardInstanceId, Is.EqualTo(99));
        }

        [Test]
        public void Fatigue_Roundtrip()
        {
            var e = new FatigueEvent(0, 3, 2);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var fe = (FatigueEvent)back;
            Assert.That(fe.Seat, Is.EqualTo(0));
            Assert.That(fe.Damage, Is.EqualTo(3));
            Assert.That(fe.FatigueCounter, Is.EqualTo(2));
        }

        [Test]
        public void Damage_WithMinionTarget_Roundtrip()
        {
            var e = new DamageEvent(10, 20, null, 5, false);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var de = (DamageEvent)back;
            Assert.That(de.SourceInstanceId, Is.EqualTo(10));
            Assert.That(de.TargetInstanceId, Is.EqualTo(20));
            Assert.That(de.TargetHeroSeat, Is.Null);
            Assert.That(de.Amount, Is.EqualTo(5));
            Assert.That(de.DivineShieldConsumed, Is.False);
        }

        [Test]
        public void Damage_WithHeroTarget_Roundtrip()
        {
            var e = new DamageEvent(null, null, 1, 7, true);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var de = (DamageEvent)back;
            Assert.That(de.SourceInstanceId, Is.Null);
            Assert.That(de.TargetInstanceId, Is.Null);
            Assert.That(de.TargetHeroSeat, Is.EqualTo(1));
            Assert.That(de.Amount, Is.EqualTo(7));
            Assert.That(de.DivineShieldConsumed, Is.True);
        }

        [Test]
        public void Healing_Roundtrip()
        {
            var e = new HealingEvent(15, null, 3);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var he = (HealingEvent)back;
            Assert.That(he.TargetInstanceId, Is.EqualTo(15));
            Assert.That(he.TargetHeroSeat, Is.Null);
            Assert.That(he.Amount, Is.EqualTo(3));
        }

        [Test]
        public void CardDeath_Roundtrip()
        {
            var e = new CardDeathEvent(77);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var cde = (CardDeathEvent)back;
            Assert.That(cde.CardInstanceId, Is.EqualTo(77));
        }

        [Test]
        public void CardPlayed_Roundtrip()
        {
            var e = new CardPlayedEvent(0, 55);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var cpe = (CardPlayedEvent)back;
            Assert.That(cpe.Seat, Is.EqualTo(0));
            Assert.That(cpe.CardInstanceId, Is.EqualTo(55));
        }

        [Test]
        public void AttackDeclared_Roundtrip()
        {
            var e = new AttackDeclaredEvent(30, 40, null);
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var ade = (AttackDeclaredEvent)back;
            Assert.That(ade.AttackerInstanceId, Is.EqualTo(30));
            Assert.That(ade.TargetInstanceId, Is.EqualTo(40));
            Assert.That(ade.TargetHeroSeat, Is.Null);
        }

        [Test]
        public void MatchEnded_Roundtrip()
        {
            var e = new MatchEndedEvent(MatchResult.Player0Wins, 0, 50, "fatigue");
            e.Sequence = 200;
            var back = EventSerializer.Deserialize(EventSerializer.Serialize(e));
            var me = (MatchEndedEvent)back;
            Assert.That(me.Result, Is.EqualTo(MatchResult.Player0Wins));
            Assert.That(me.WinnerId, Is.EqualTo(0));
            Assert.That(me.TurnNumber, Is.EqualTo(50));
            Assert.That(me.Reason, Is.EqualTo("fatigue"));
            Assert.That(me.Sequence, Is.EqualTo(200));
        }

        // -----------------------------------------------------------------
        // 边界 / 错误路径
        // -----------------------------------------------------------------

        [Test]
        public void UnknownTypeLabel_ThrowsArgumentException()
        {
            var json = "{\"type\":\"Bogus\",\"sequence\":0}";
            Assert.Throws<ArgumentException>(() => EventSerializer.Deserialize(json));
        }

        [Test]
        public void MissingType_ThrowsArgumentException()
        {
            var json = "{\"sequence\":0}";
            Assert.Throws<ArgumentException>(() => EventSerializer.Deserialize(json));
        }

        // -----------------------------------------------------------------
        // MatchHost 集成：step 消息 events 字段含完整事件对象
        // -----------------------------------------------------------------

        [Test]
        public void MatchHost_StepEvents_AreFullEventObjects()
        {
            var database = MatchControllerFixtures.BuildDatabase();
            var deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            var controller = new MatchController(
                MatchControllerFixtures.NewMatch(database, deck, seed: 42),
                database);

            var host = new Card.Application.Match.MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);
            // M13-T2 座位归属校验：端点须挂实际行动座位（seed 42 先手非固定座位 0）
            host.Attach(serverEp, controller.State.ActivePlayerId);

            // hello → 快照
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            clientEp.TryReceive(out _);

            // EndTurn → step
            SendCommand(clientEp, new EndTurnCommand(controller.State.ActivePlayerId));
            host.Pump();

            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True);
            JsonValue stepJson = JsonValue.Parse(stepRaw!);
            Assert.That(stepJson.TryGetMember("events", out JsonValue events), Is.True);
            Assert.That(events.Items.Count, Is.GreaterThan(0));

            // 第一个事件应为完整对象（含 type + sequence + 字段）
            JsonValue first = events.Items[0];
            Assert.That(first.TryGetMember("type", out JsonValue type), Is.True);
            Assert.That(type.Kind, Is.EqualTo(JsonKind.String));
            Assert.That(first.TryGetMember("sequence", out JsonValue seq), Is.True);
            Assert.That(seq.Kind, Is.EqualTo(JsonKind.Number));

            // 往返反序列化成功
            GameEvent back = EventSerializer.ReadEvent(first);
            Assert.That(back.Sequence, Is.EqualTo(seq.IntValue));
        }

        private static void SendCommand(LoopbackEndpoint endpoint, IGameCommand command)
        {
            string payloadJson = CommandSerializer.Serialize(command);
            JsonValue payload = JsonValue.Parse(payloadJson);
            JsonValue msg = JsonValue.Object()
                .Add("kind", JsonValue.From("command"))
                .Add("payload", payload);
            endpoint.Send(msg.ToJson());
        }
    }
}
