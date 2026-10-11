using System;
using System.Text;
using System.Threading;
using Card.Network;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// BclTcpTransport 测试（M12-T3）：真实 TCP 回环连接，验证 Accept/Connect/Send/Receive。
    /// </summary>
    [TestFixture]
    public class BclTcpTransportTests
    {
        [Test]
        public void Listen_Accept_Connect_EstablishesPair()
        {
            using var listener = BclTcpTransport.Listen(0); // 0 = 让 OS 分配端口
            int port = listener.LocalEndPoint.Port;

            var acceptThread = new Thread(() =>
            {
                using var endpoint = listener.Accept();
                // 保持连接直到测试结束
                Thread.Sleep(500);
            }) { IsBackground = true };
            acceptThread.Start();

            using var client = BclTcpTransport.Connect("127.0.0.1", port);
            Assert.That(client.Connected, Is.True);

            acceptThread.Join(2000);
        }

        [Test]
        public void Send_And_Receive_FullFrame()
        {
            using var listener = BclTcpTransport.Listen(0);
            int port = listener.LocalEndPoint.Port;

            ITransport? serverEp = null;
            var acceptThread = new Thread(() =>
            {
                serverEp = listener.Accept();
            }) { IsBackground = true };
            acceptThread.Start();

            using var client = BclTcpTransport.Connect("127.0.0.1", port);
            acceptThread.Join(2000);
            Assert.That(serverEp, Is.Not.Null);

            // 客户端发一条完整帧
            byte[] frame = MessageCodec.Frame(
                NetworkMessageType.Hello,
                MessageCodec.CurrentProtocolVersion,
                "{\"playerName\":\"test\"}");
            client.Send(frame);

            // 服务端自旋收（后台线程可能还在 Read 等待）
            byte[]? received = null;
            for (int i = 0; i < 50 && received == null; i++)
            {
                if (serverEp!.TryReceive(out byte[]? r)) received = r;
                else Thread.Sleep(10);
            }

            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Length, Is.EqualTo(frame.Length));
            Assert.That(BitConverter.ToInt32(received, 0), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void Dispose_Client_ServerDetectsDisconnect()
        {
            using var listener = BclTcpTransport.Listen(0);
            int port = listener.LocalEndPoint.Port;

            ITransport? serverEp = null;
            bool serverDisconnected = false;
            var acceptThread = new Thread(() =>
            {
                serverEp = listener.Accept();
                if (serverEp != null) serverEp.OnDisconnected += () => serverDisconnected = true;
            }) { IsBackground = true };
            acceptThread.Start();

            var client = BclTcpTransport.Connect("127.0.0.1", port);
            acceptThread.Join(2000);
            Assert.That(serverEp, Is.Not.Null);

            // 客户端关闭
            client.Dispose();

            // 服务端应检测到断开
            int attempts = 0;
            while (!serverDisconnected && attempts < 10)
            {
                Thread.Sleep(100);
                attempts++;
            }

            Assert.That(serverDisconnected, Is.True);
            Assert.That(serverEp!.Connected, Is.False);
        }

        [Test]
        public void PeerReset_NoExceptionEscapes_ServerDetectsDisconnect()
        {
            // M14-T3 复跑暴露：对端 RST（LingerState(true,0) 关闭）时本端 _connected 仍为
            // true，"when (!_connected)" 过滤器失效 → IOException 逃逸接收线程顶层
            // （Unity 记 Exception 日志判测试失败，即 M12-B2 修复不完整）。防回归：
            // 对端 RST 必须静默断链 + 触发 Disconnected，不产生线程逃逸异常。
            using var listener = BclTcpTransport.Listen(0);
            int port = listener.LocalEndPoint.Port;

            ITransport? serverEp = null;
            bool serverDisconnected = false;
            var acceptThread = new Thread(() =>
            {
                serverEp = listener.Accept();
                if (serverEp != null) serverEp.OnDisconnected += () => serverDisconnected = true;
            }) { IsBackground = true };
            acceptThread.Start();

            // 原始 socket 客户端（不经 BclTcpTransport，避免其接收线程干扰断言）：
            // 连接后以 RST 方式关闭
            using var raw = new System.Net.Sockets.TcpClient();
            raw.Connect("127.0.0.1", port);
            acceptThread.Join(2000);
            Assert.That(serverEp, Is.Not.Null);

            raw.LingerState = new System.Net.Sockets.LingerOption(true, 0);
            raw.Close();

            int attempts = 0;
            while (!serverDisconnected && attempts < 30)
            {
                Thread.Sleep(50);
                attempts++;
            }

            Assert.That(serverDisconnected, Is.True, "对端 RST 后服务端应触发断链事件");
            Assert.That(serverEp!.Connected, Is.False);
        }

        [Test]
        public void MultipleMessages_SequentialReceive()
        {
            using var listener = BclTcpTransport.Listen(0);
            int port = listener.LocalEndPoint.Port;

            ITransport? serverEp = null;
            var acceptThread = new Thread(() => { serverEp = listener.Accept(); }) { IsBackground = true };
            acceptThread.Start();

            using var client = BclTcpTransport.Connect("127.0.0.1", port);
            acceptThread.Join(2000);

            // 客户端连发 3 帧
            for (int i = 0; i < 3; i++)
            {
                byte[] frame = MessageCodec.Frame(
                    NetworkMessageType.Command,
                    MessageCodec.CurrentProtocolVersion,
                    $"{{\"seq\":{i}}}");
                client.Send(frame);
            }

            // 服务端自旋逐条收
            int received = 0;
            for (int i = 0; i < 3; i++)
            {
                bool got = false;
                for (int tryCount = 0; tryCount < 50 && !got; tryCount++)
                {
                    if (serverEp!.TryReceive(out byte[]? _)) { got = true; received++; }
                    else Thread.Sleep(10);
                }
            }

            Assert.That(received, Is.EqualTo(3));
        }
    }
}
