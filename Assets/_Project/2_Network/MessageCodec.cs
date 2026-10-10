using System;
using System.IO;
using System.Text;

namespace Card.Network
{
    /// <summary>
    /// 网络消息类型（M12-T4；Docs/05 §9）。
    /// C→S 与 S→C 通用；方向见 <see cref="MessageDirection"/>。
    /// </summary>
    public enum NetworkMessageType
    {
        // 握手
        Hello = 1,           // C→S：协议版本 + 玩家名
        HelloAck = 2,        // S→C：协议版本确认 + 房主玩家
        Ready = 3,           // C→S：准备
        MatchStart = 4,      // S→C：对局开始（权威信息）

        // 对局中
        Command = 10,        // C→S：GameCommand（序号+回合+内容）
        StateDelta = 11,     // S→C：版本 + 增量 + 事件
        MatchSnapshot = 12,  // S→C：全量快照（重连/版本冲突）

        // 结束/错误
        MatchEnd = 20,       // S→C：胜负 + 结束原因
        Error = 21,          // S→C：错误码 + 说明
        Leave = 22,          // C→S：主动退出

        // 心跳（M12-T7）
        Ping = 30,           // C→S：时间戳
        Pong = 31,           // S→C：回显时间戳
    }

    /// <summary>
    /// 消息编解码器（M12-T4）：长度前缀 framing + 消息类型标签 + UTF-8 JSON 载荷。
    /// 协议版本不匹配时抛 <see cref="ProtocolException"/>；畸形包同样抛异常。
    /// 独立于传输层：<see cref="InMemoryTransport"/> 绕过 framing（直接传 RawMessage），
    /// <see cref="BclTcpTransport"/> 调用 <see cref="Frame"/>/<see cref="Unframe"/>。
    /// </summary>
    public static class MessageCodec
    {
        /// <summary>当前协议版本——两端必须匹配才能握手。</summary>
        public const int CurrentProtocolVersion = 1;

        /// <summary>单条消息最大载荷（256 KB——快照不应超过此值）。</summary>
        public const int MaxFrameSize = 256 * 1024;

        /// <summary>
        /// 将 <see cref="RawMessage"/> 帧化为字节数组：4 字节 LE 长度 + 1 字节类型 + 4 字节协议版本 + UTF-8 载荷。
        /// </summary>
        public static byte[] Frame(NetworkMessageType type, int protocolVersion, string payloadJson)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            int totalLen = 1 + 4 + payloadBytes.Length; // type(1) + version(4) + payload

            if (totalLen > MaxFrameSize)
            {
                throw new ArgumentException($"消息帧过大：{totalLen} > {MaxFrameSize}");
            }

            using var ms = new MemoryStream(totalLen + 4); // 前缀长度(4) + 帧内容
            using var writer = new BinaryWriter(ms);
            writer.Write(totalLen);              // 4 字节 LE：帧体长度
            writer.Write((byte)type);            // 1 字节：消息类型
            writer.Write(protocolVersion);       // 4 字节 LE：协议版本
            writer.Write(payloadBytes);          // 载荷
            return ms.ToArray();
        }

        /// <summary>
        /// 从字节流中读取一条完整消息；数据不足时返回 null（非阻塞）。
        /// 长度前缀只包含帧体（不含前缀自身）——4 + 帧体 = 完整包大小。
        /// </summary>
        public static RawMessage? TryUnframe(byte[] buffer, int offset, int available, out int consumed)
        {
            consumed = 0;
            if (available < 4) return null; // 不够读前缀

            int frameBodyLen = BitConverter.ToInt32(buffer, offset);
            if (frameBodyLen < 5) // type(1) + version(4) 最小
            {
                throw new ProtocolException($"畸形帧体长度：{frameBodyLen}");
            }

            int totalLen = 4 + frameBodyLen;
            if (frameBodyLen > MaxFrameSize)
            {
                throw new ProtocolException($"帧体过大：{frameBodyLen} > {MaxFrameSize}");
            }

            if (available < totalLen) return null; // 数据不足

            NetworkMessageType type = (NetworkMessageType)buffer[offset + 4];
            int version = BitConverter.ToInt32(buffer, offset + 5);
            string payload = Encoding.UTF8.GetString(buffer, offset + 9, frameBodyLen - 5);

            consumed = totalLen;
            return new RawMessage(type, version, payload);
        }
    }

    /// <summary>解帧后的原始消息：类型 + 协议版本 + JSON 载荷。</summary>
    public readonly struct RawMessage
    {
        public NetworkMessageType Type { get; }
        public int ProtocolVersion { get; }
        public string PayloadJson { get; }

        public RawMessage(NetworkMessageType type, int protocolVersion, string payloadJson)
        {
            Type = type;
            ProtocolVersion = protocolVersion;
            PayloadJson = payloadJson;
        }
    }

    /// <summary>协议异常：版本不匹配 / 畸形包 / 未知消息类型等。</summary>
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message) { }
    }
}
