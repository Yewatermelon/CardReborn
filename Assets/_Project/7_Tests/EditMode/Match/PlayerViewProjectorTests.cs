using System.Collections.Generic;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>视野裁剪测试（M13-T6）：对手手牌只留数量与背面标识；牌库顺序对任何视角不下发；公开区完整。</summary>
    [TestFixture]
    public class PlayerViewProjectorTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;
        private MatchController _controller = null!;
        private MatchHost _host = null!;
        private LoopbackEndpoint _s0 = null!, _s1 = null!, _c0 = null!, _c1 = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        private MatchController NewController()
            => new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);

        private static string CommandEnvelope(int seq, string payloadJson)
            => JsonValue.Object()
                    .Add("kind", JsonValue.From("command"))
                    .Add("seq", JsonValue.From(seq))
                    .Add("payload", JsonValue.Parse(payloadJson))
                    .ToJson();

        private static string EndTurnPayload(int seat)
            => CommandSerializer.Serialize(new EndTurnCommand(seat));

        private static JsonValue HelloSnapshot(MatchHost host, LoopbackEndpoint clientEp)
        {
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? snapRaw), Is.True, "hello 应回快照");
            return JsonValue.Parse(snapRaw!);
        }

        /// <summary>发送命令并 Pump，返回（发送端 step，另一端 step）——两份按各自座位裁剪。</summary>
        private static JsonValue[] BroadcastSteps(
            LoopbackEndpoint senderEp, LoopbackEndpoint otherEp, MatchHost host, string raw)
        {
            senderEp.Send(raw);
            host.Pump();
            bool toSender = senderEp.TryReceive(out string? s);
            bool toOther = otherEp.TryReceive(out string? o);
            Assert.That(toSender && toOther, Is.True, "step 应回达两端点");
            return new[] { JsonValue.Parse(s!), JsonValue.Parse(o!) };
        }

        private LoopbackEndpoint Ep(int seat) => seat == 0 ? _c0 : _c1;

        private static JsonValue PlayerZone(JsonValue snapshot, int seat, string zone)
        {
            Assert.That(snapshot.TryGetMember("state", out JsonValue state), Is.True);
            Assert.That(state.TryGetMember("players", out JsonValue players), Is.True);
            JsonValue player = players.Items[seat];
            Assert.That(player.TryGetMember("zones", out JsonValue zones), Is.True, "player 应含 zones");
            Assert.That(zones.TryGetMember(zone, out JsonValue cards), Is.True, $"players[{seat}].{zone} 应存在");
            return cards;
        }

        [Test]
        public void Project_MasksOpponentHand_KeepsCountAndOwnDetails()
        {
            // AC-1：对手 hand 数量保留、全为背面占位；自己 hand 明细完整
            _controller = NewController();
            MatchState view = PlayerViewProjector.Project(_controller.State, observerPlayerId: 0);
            PlayerState mine = _controller.State.GetPlayer(0);
            PlayerState theirs = _controller.State.GetPlayer(1);

            Assert.That(view.GetPlayer(1).Hand.Count, Is.EqualTo(theirs.Hand.Count), "对手手牌数量保留");
            foreach (CardInstance masked in view.GetPlayer(1).Hand.Cards)
            {
                Assert.That(masked.CardKey, Is.EqualTo(PlayerViewProjector.MaskedCardKey), "对手手牌内容应抹除");
            }

            Assert.That(view.GetPlayer(0).Hand.Count, Is.EqualTo(mine.Hand.Count));
            for (int i = 0; i < mine.Hand.Count; i++)
            {
                Assert.That(view.GetPlayer(0).Hand.Cards[i].CardKey, Is.EqualTo(mine.Hand.Cards[i].CardKey),
                    "自己手牌明细应完整");
                Assert.That(view.GetPlayer(0).Hand.Cards[i].InstanceId, Is.EqualTo(mine.Hand.Cards[i].InstanceId));
            }
        }

        [Test]
        public void Project_MasksBothDecks_OrderAndContentNeverLeavesHost()
        {
            // AC-2：双方牌库在任意视角下均为占位卡，数量保留
            foreach (int observer in new[] { 0, 1 })
            {
                MatchState view = PlayerViewProjector.Project(_controller.State, observerPlayerId: observer);
                foreach (int seat in new[] { 0, 1 })
                {
                    PlayerState authority = _controller.State.GetPlayer(seat);
                    Assert.That(view.GetPlayer(seat).Deck.Count, Is.EqualTo(authority.Deck.Count),
                        $"observer={observer} 应看到 seat={seat} 牌库数量");
                    foreach (CardInstance masked in view.GetPlayer(seat).Deck.Cards)
                    {
                        Assert.That(masked.CardKey, Is.EqualTo(PlayerViewProjector.MaskedCardKey),
                            $"observer={observer} 不应看到 seat={seat} 牌库内容");
                    }
                }
            }
        }

        [Test]
        public void Project_KeepsPublicZonesFull()
        {
            // AC-3：board 卡在对手视角下 CardKey/InstanceId 与权威一致；英雄/法力数值不变
            PlayerState p0 = _controller.State.GetPlayer(0);
            CardInstance played = p0.Hand.Cards[0];
            Assert.That(p0.Board.MoveIn(played, p0.Hand).IsSuccess, Is.True, "测试布置：手牌上场");

            MatchState view = PlayerViewProjector.Project(_controller.State, observerPlayerId: 1);
            Assert.That(view.GetPlayer(0).Board.Count, Is.EqualTo(1));
            Assert.That(view.GetPlayer(0).Board.Cards[0].CardKey, Is.EqualTo(played.CardKey));
            Assert.That(view.GetPlayer(0).Board.Cards[0].InstanceId, Is.EqualTo(played.InstanceId));
            Assert.That(view.GetPlayer(0).Hero.Health, Is.EqualTo(p0.Hero.Health));
            Assert.That(view.GetPlayer(0).Mana.Current, Is.EqualTo(p0.Mana.Current));
        }

        /// <summary>校验 step 中对手隐藏分区（hand/deck）的裁剪语义：分区级 Added 载荷为占位卡；禁止元素级变化泄露。</summary>
        private static void AssertOpponentHiddenZonesMasked(JsonValue step, int otherSeat)
        {
            Assert.That(step.TryGetMember("changes", out JsonValue changes), Is.True, "step 应含 changes");
            foreach (JsonValue change in changes.Items)
            {
                Assert.That(change.TryGetMember("path", out JsonValue path), Is.True);
                string p = path.StringValue;
                string handPath = $"players[{otherSeat}].hand";
                string deckPath = $"players[{otherSeat}].deck";
                bool zoneLevel = p == handPath || p == deckPath;
                bool elementLevel = p.StartsWith(handPath + "[") || p.StartsWith(deckPath + "[");
                if (elementLevel)
                {
                    Assert.Fail($"对手隐藏分区不应有元素级变化（会泄露牌面/顺序）：{p}");
                }

                if (zoneLevel)
                {
                    Assert.That(change.TryGetMember("kind", out JsonValue kind), Is.True);
                    if (kind.StringValue != "Added")
                    {
                        continue; // Removed 载荷只有 instanceId，无内容
                    }

                    Assert.That(change.TryGetMember("new", out JsonValue added), Is.True);
                    Assert.That(added.TryGetMember("cardKey", out JsonValue key), Is.True);
                    Assert.That(key.StringValue, Is.EqualTo(PlayerViewProjector.MaskedCardKey),
                        $"对手分区 {p} 的 Added 载荷应为占位卡");
                }
            }
        }

        /// <summary>校验快照中某座位隐藏分区的裁剪语义：数量保留、全为占位卡。</summary>
        private static void AssertSnapshotZoneMasked(JsonValue snapshot, int seat, string zone, int expectedCount)
        {
            JsonValue cards = PlayerZone(snapshot, seat, zone);
            Assert.That(cards.Items.Count, Is.EqualTo(expectedCount), $"players[{seat}].{zone} 数量应保留");
            foreach (JsonValue card in cards.Items)
            {
                Assert.That(card.TryGetMember("cardKey", out JsonValue key)
                    && key.StringValue == PlayerViewProjector.MaskedCardKey, Is.True,
                    $"players[{seat}].{zone} 应全为占位卡");
            }
        }

        [Test]
        public void HostSteps_PerSeatTrimmed_NoOpponentSecretKeys()
        {
            // AC-4：端到端——A 结束回合后，A 的 step/快照中 B 的手牌与牌库全为占位卡，B 的亦然；
            // 对手隐藏分区无元素级变化（防牌面/顺序泄露）；数量语义保留。
            // 注：双方牌库同表（CardKey 重叠），故用结构化断言而非字符串包含检测。
            _controller = NewController();
            _host = new MatchHost(_controller);
            LoopbackLink.CreatePair(out _s0, out _c0);
            LoopbackLink.CreatePair(out _s1, out _c1);
            _host.Attach(_s0, 0);
            _host.Attach(_s1, 1);
            HelloSnapshot(_host, _c0);
            HelloSnapshot(_host, _c1);

            int a = _controller.State.ActivePlayerId;
            int b = 1 - a;
            int bDeckCount = _controller.State.GetPlayer(b).Deck.Count;

            JsonValue[] steps = BroadcastSteps(Ep(a), Ep(b), _host, CommandEnvelope(1, EndTurnPayload(a)));
            AssertOpponentHiddenZonesMasked(steps[0], b); // A 收到的 step：B 分区须遮蔽
            AssertOpponentHiddenZonesMasked(steps[1], a); // B 收到的 step：A 分区须遮蔽

            JsonValue snapA = HelloSnapshot(_host, _c0);
            JsonValue snapB = HelloSnapshot(_host, _c1);
            AssertSnapshotZoneMasked(snapA, b, "hand", _controller.State.GetPlayer(b).Hand.Count);
            AssertSnapshotZoneMasked(snapA, b, "deck", _controller.State.GetPlayer(b).Deck.Count);
            AssertSnapshotZoneMasked(snapB, a, "hand", _controller.State.GetPlayer(a).Hand.Count);
            AssertSnapshotZoneMasked(snapB, a, "deck", _controller.State.GetPlayer(a).Deck.Count);
            Assert.That(bDeckCount, Is.GreaterThan(0), "前置：牌库非空才有遮蔽意义");
        }
    }
}
