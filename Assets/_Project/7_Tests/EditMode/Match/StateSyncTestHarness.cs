using System.Collections.Generic;
using Card.Core;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// 状态同步测试夹具助手（M13-T6 自 ClientStateSyncTests 拆出）：
    /// 对局构造、命令信封、快照/步收取、座位副本选择（M13-T6 起 step 按座位裁剪）。
    /// 测试类内以 `using static Card.Tests.EditMode.Match.StateSyncTestHarness;` 复用，调用点零改动。
    /// </summary>
    internal static class StateSyncTestHarness
    {
        public static MatchController NewController()
        {
            CardDatabase database = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            return new MatchController(
                MatchControllerFixtures.NewMatch(database, deck, seed: 42), database);
        }

        public static string CommandEnvelope(int seq, string payloadJson)
            => JsonValue.Object()
                    .Add("kind", JsonValue.From("command"))
                    .Add("seq", JsonValue.From(seq))
                    .Add("payload", JsonValue.Parse(payloadJson))
                    .ToJson();

        public static string EndTurnPayload(int seat)
            => CommandSerializer.Serialize(new EndTurnCommand(seat));

        public static string HelloSnapshot(MatchHost host, LoopbackEndpoint clientEp)
        {
            clientEp.Send("{\"kind\":\"hello\"}");
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? snapRaw), Is.True, "hello 应回快照");
            return snapRaw!;
        }

        public static ClientStateSync NewSync(MatchHost host, LoopbackEndpoint clientEp)
        {
            ClientStateSync sync = new ClientStateSync();
            sync.ApplySnapshot(JsonValue.Parse(HelloSnapshot(host, clientEp)));
            return sync;
        }

        public static JsonValue SendStep(LoopbackEndpoint clientEp, MatchHost host, string raw)
        {
            clientEp.Send(raw);
            host.Pump();
            Assert.That(clientEp.TryReceive(out string? stepRaw), Is.True, "应产出 step 消息");
            return JsonValue.Parse(stepRaw!);
        }

        /// <summary>发送命令并 Pump，按参数顺序返回（发送端副本, 另一端副本）——两份按各自座位裁剪。</summary>
        public static JsonValue[] BroadcastSteps(
            LoopbackEndpoint senderEp, LoopbackEndpoint otherEp, MatchHost host, string raw)
        {
            senderEp.Send(raw);
            host.Pump();
            bool toSender = senderEp.TryReceive(out string? s);
            bool toOther = otherEp.TryReceive(out string? o);
            Assert.That(toSender && toOther, Is.True, "step 应回达两端点");
            return new[] { JsonValue.Parse(s!), JsonValue.Parse(o!) };
        }

        public static bool IsAccepted(JsonValue step)
            => step.TryGetMember("accepted", out JsonValue accepted) && accepted.BoolValue;

        /// <summary>从（发送端副本, 另一端副本）中取座位 seat 端点的 step 副本。</summary>
        public static JsonValue StepForSeat(int seat, int senderSeat, JsonValue[] steps)
            => seat == senderSeat ? steps[0] : steps[1];

        public static ulong MemberNumber(JsonValue step, string key)
        {
            Assert.That(step.TryGetMember(key, out JsonValue value) && value.Kind == JsonKind.Number,
                Is.True, $"step 应含数字字段 {key}");
            return (ulong)value.IntValue;
        }

        public static int EventCount(JsonValue step)
            => step.TryGetMember("events", out JsonValue events) ? events.Items.Count : 0;
    }
}
