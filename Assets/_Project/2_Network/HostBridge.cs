using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;

namespace Card.Network
{
    /// <summary>
    /// 宿主桥接器（M13-T1）：把权威 <see cref="MatchHost"/> 的一个回环端点与一条网络传输对接。
    /// 上行：PeerSession 解帧后的 Command 消息经 <see cref="ForwardCommand"/> 注入权威上行；
    /// 下行：权威产出的 snapshot/step 经 <see cref="PumpKernelToNetwork"/> 帧化为
    /// MatchSnapshot / StateDelta 帧发出。
    /// 畸形与未知消息一律丢弃并记日志，不让权威宿主因单个坏包停摆（Docs/05 §8.3）。
    /// </summary>
    public sealed class HostBridge
    {
        private readonly LoopbackEndpoint _kernelEndpoint;
        private readonly ITransport _transport;
        private readonly int _seat;
        private readonly NetworkDiagnostics? _diag;

        public HostBridge(LoopbackEndpoint kernelEndpoint, ITransport transport, int seat, NetworkDiagnostics? diag = null)
        {
            _kernelEndpoint = Guard.NotNull(kernelEndpoint, nameof(kernelEndpoint));
            _transport = Guard.NotNull(transport, nameof(transport));
            _seat = seat;
            _diag = diag;
        }

        /// <summary>
        /// 对局开始通知：向权威注入 hello 上行，权威下一次 Pump 会回一条按座位投影的开局快照。
        /// </summary>
        public void NotifyMatchStarted()
        {
            _kernelEndpoint.Send("{\"kind\":\"hello\"}");
        }

        /// <summary>
        /// 转发已解帧的 Command 消息到权威上行。载荷非法（畸形 JSON / 未知 kind）时丢弃并记日志。
        /// </summary>
        public void ForwardCommand(RawMessage message)
        {
            if (!TryValidateUplink(message.PayloadJson, out string reason))
            {
                NetworkLogger.Warn($"HostBridge 丢弃非法 Command 帧：seat={_seat}，原因：{reason}");
                return;
            }

            _kernelEndpoint.Send(message.PayloadJson);
        }

        /// <summary>
        /// 把权威下行（快照 / 步进）帧化发送到网络；传输已断开时立即停止。返回发送条数。
        /// </summary>
        public int PumpKernelToNetwork()
        {
            int sent = 0;
            while (_kernelEndpoint.TryReceive(out string raw))
            {
                if (!_transport.Connected)
                {
                    break;
                }

                if (TryEgressOne(raw))
                {
                    sent++;
                }
            }

            return sent;
        }

        // -----------------------------------------------------------------
        // 私有：帧校验与下行分发
        // -----------------------------------------------------------------

        private static bool TryValidateUplink(string payloadJson, out string reason)
        {
            reason = string.Empty;
            JsonValue json;
            try
            {
                json = JsonValue.Parse(payloadJson);
            }
            catch (FormatException ex)
            {
                reason = "载荷不是合法 JSON：" + ex.Message;
                return false;
            }

            if (!json.TryGetMember("kind", out JsonValue kind) || kind.Kind != JsonKind.String)
            {
                reason = "缺少 kind 字段。";
                return false;
            }

            if (kind.StringValue != "command")
            {
                reason = "未知 kind：\"" + kind.StringValue + "\"。";
                return false;
            }

            return true;
        }

        private bool TryEgressOne(string raw)
        {
            JsonValue json;
            try
            {
                json = JsonValue.Parse(raw);
            }
            catch (FormatException ex)
            {
                NetworkLogger.Warn($"HostBridge 丢弃内核下行畸形消息：seat={_seat}，原因：{ex.Message}");
                return false;
            }

            if (!json.TryGetMember("kind", out JsonValue kind) || kind.Kind != JsonKind.String)
            {
                NetworkLogger.Warn($"HostBridge 内核下行消息缺少 kind：seat={_seat}");
                return false;
            }

            NetworkMessageType type;
            switch (kind.StringValue)
            {
                case "snapshot":
                    type = NetworkMessageType.MatchSnapshot;
                    break;
                case "step":
                    type = NetworkMessageType.StateDelta;
                    break;
                default:
                    NetworkLogger.Warn($"HostBridge 未知内核下行 kind：\"{kind.StringValue}\"，seat={_seat}");
                    return false;
            }

            _transport.Send(MessageCodec.Frame(type, MessageCodec.CurrentProtocolVersion, raw));
            _diag?.Record(NetworkEventType.SendFrame, $"type={type}, seat={_seat}");
            return true;
        }
    }
}
