using System;
using System.Text;
using Card.Network;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// MessageCodec 测试（M12-T4）：长度前缀 framing 往返 + 协议版本 + 畸形包拒绝。
    /// </summary>
    [TestFixture]
    public class MessageCodecTests
    {
        [Test]
        public void Frame_Unframe_Roundtrip()
        {
            byte[] framed = MessageCodec.Frame(
                NetworkMessageType.Hello,
                MessageCodec.CurrentProtocolVersion,
                "{\"playerName\":\"test\"}");

            RawMessage? result = MessageCodec.TryUnframe(framed, 0, framed.Length, out int consumed);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.Type, Is.EqualTo(NetworkMessageType.Hello));
            Assert.That(result.Value.ProtocolVersion, Is.EqualTo(MessageCodec.CurrentProtocolVersion));
            Assert.That(result.Value.PayloadJson, Is.EqualTo("{\"playerName\":\"test\"}"));
            Assert.That(consumed, Is.EqualTo(framed.Length));
        }

        [Test]
        public void Frame_VersionIsEmbedded()
        {
            byte[] framed = MessageCodec.Frame(NetworkMessageType.Ready, 42, "{}");
            RawMessage? result = MessageCodec.TryUnframe(framed, 0, framed.Length, out _);
            Assert.That(result!.Value.ProtocolVersion, Is.EqualTo(42));
        }

        [Test]
        public void TryUnframe_Data不足_ReturnsNull()
        {
            byte[] framed = MessageCodec.Frame(NetworkMessageType.Hello, 1, "{}");
            // 只给前 3 字节——不够读长度前缀
            RawMessage? result = MessageCodec.TryUnframe(framed, 0, 3, out int consumed);
            Assert.That(result, Is.Null);
            Assert.That(consumed, Is.EqualTo(0));
        }

        [Test]
        public void TryUnframe_帧体不够_ReturnsNull()
        {
            byte[] framed = MessageCodec.Frame(NetworkMessageType.Hello, 1, "{\"a\":\"b\"}");
            // 给前缀 + 部分帧体
            RawMessage? result = MessageCodec.TryUnframe(framed, 0, 4 + 3, out int consumed);
            Assert.That(result, Is.Null);
            Assert.That(consumed, Is.EqualTo(0));
        }

        [Test]
        public void TryUnframe_畸形帧体长度_ThrowsProtocolException()
        {
            byte[] bad = new byte[] { 0x01, 0x00, 0x00, 0x00 }; // 长度=1，但帧体最小是 5
            Assert.Throws<ProtocolException>(() =>
                MessageCodec.TryUnframe(bad, 0, bad.Length, out _));
        }

        [Test]
        public void TryUnframe_帧体过大_ThrowsProtocolException()
        {
            byte[] bad = new byte[4];
            BitConverter.GetBytes(MessageCodec.MaxFrameSize + 1).CopyTo(bad, 0);
            Assert.Throws<ProtocolException>(() =>
                MessageCodec.TryUnframe(bad, 0, bad.Length, out _));
        }

        [Test]
        public void Frame_载荷过大_ThrowsArgumentException()
        {
            string huge = new string('x', MessageCodec.MaxFrameSize);
            Assert.Throws<ArgumentException>(() =>
                MessageCodec.Frame(NetworkMessageType.Hello, 1, huge));
        }

        [Test]
        public void TryUnframe_多帧连续_逐帧消费()
        {
            byte[] frame1 = MessageCodec.Frame(NetworkMessageType.Hello, 1, "{}");
            byte[] frame2 = MessageCodec.Frame(NetworkMessageType.Ready, 1, "{}");

            byte[] combined = new byte[frame1.Length + frame2.Length];
            Buffer.BlockCopy(frame1, 0, combined, 0, frame1.Length);
            Buffer.BlockCopy(frame2, 0, combined, frame1.Length, frame2.Length);

            int offset = 0;
            var r1 = MessageCodec.TryUnframe(combined, offset, combined.Length - offset, out int c1);
            Assert.That(r1!.Value.Type, Is.EqualTo(NetworkMessageType.Hello));
            offset += c1;
            Assert.That(offset, Is.EqualTo(frame1.Length));

            var r2 = MessageCodec.TryUnframe(combined, offset, combined.Length - offset, out int c2);
            Assert.That(r2!.Value.Type, Is.EqualTo(NetworkMessageType.Ready));
            offset += c2;
            Assert.That(offset, Is.EqualTo(combined.Length));
        }
    }
}
