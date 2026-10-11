using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// MatchHost 座位化挂载测试（M13-T1）：Attach 携带座位 Id，初始快照按座位投影。
    /// </summary>
    [TestFixture]
    public class MatchHostSeatTests
    {
        private CardDatabase _database = null!;
        private IReadOnlyList<string> _deck = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _database = MatchControllerFixtures.BuildDatabase();
            _deck = MatchControllerFixtures.LoadEnabledDeckKeys();
        }

        private MatchController NewController()
        {
            return new MatchController(
                MatchControllerFixtures.NewMatch(_database, _deck, seed: 42), _database);
        }

        // -----------------------------------------------------------------
        // AC-1: Attach(endpoint, seat) 记忆座位；hello → 快照按该座位投影
        // -----------------------------------------------------------------

        [Test]
        public void Attach_WithSeat_RemembersSeatAndSendsSnapshot()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);

            host.Attach(serverEp, 1);

            Assert.That(host.AttachedSeats, Does.Contain(1));

            clientEp.Send("{\"kind\":\"hello\"}");
            Assert.DoesNotThrow(() => host.Pump());

            Assert.That(clientEp.TryReceive(out string? raw), Is.True);
            JsonValue json = JsonValue.Parse(raw!);
            Assert.That(json.TryGetMember("kind", out JsonValue kind) && kind.StringValue == "snapshot", Is.True);
        }

        // -----------------------------------------------------------------
        // 兼容：旧单参 Attach 默认座位 0（M11 测试与 LocalHost 路径不受影响）
        // -----------------------------------------------------------------

        [Test]
        public void Attach_DefaultOverload_SeatIsZero()
        {
            MatchController controller = NewController();
            MatchHost host = new MatchHost(controller);
            LoopbackLink.CreatePair(out LoopbackEndpoint serverEp, out LoopbackEndpoint clientEp);

            host.Attach(serverEp);

            Assert.That(host.AttachedSeats, Does.Contain(0));
        }
    }
}
