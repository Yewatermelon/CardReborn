using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Card.Network
{
    /// <summary>
    /// BCL TCP 传输（M12-T3；Docs/05 §3.3）：System.Net.Sockets + MessageCodec framing。
    /// 零第三方依赖，本阶段用于本机双进程直连（127.0.0.1）。
    /// </summary>
    public static class BclTcpTransport
    {
        /// <summary>创建客户端——连接到房主地址。</summary>
        public static BclTcpClient Connect(string host, int port)
        {
            var client = new TcpClient();
            client.Connect(host, port);
            return new BclTcpClient(client);
        }

        /// <summary>创建宿主监听——房主侧，Accept 新连接后返回 <see cref="BclTcpTransportEndpoint"/>。</summary>
        public static BclTcpHostListener Listen(int port)
        {
            return new BclTcpHostListener(port);
        }
    }

    /// <summary>宿主监听：Accept 新 TCP 连接。</summary>
    public sealed class BclTcpHostListener : IDisposable
    {
        private readonly TcpListener _listener;
        private bool _disposed;

        public BclTcpHostListener(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
        }

        /// <summary>监听地址（本机）。</summary>
        public IPEndPoint LocalEndPoint => (IPEndPoint)_listener.LocalEndpoint;

        /// <summary>Accept 一条连接（阻塞）；断开或 Dispose 时返回 null。</summary>
        public BclTcpTransportEndpoint? Accept()
        {
            try
            {
                TcpClient tcp = _listener.AcceptTcpClient();
                return new BclTcpTransportEndpoint(tcp);
            }
            catch (SocketException) when (_disposed)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { _listener.Stop(); } catch { }
            }
        }
    }

    /// <summary>客户端侧：Connect 后返回的 <see cref="ITransport"/>。</summary>
    public sealed class BclTcpClient : ITransport
    {
        private readonly BclTcpTransportEndpoint _inner;

        public BclTcpClient(TcpClient tcp)
        {
            _inner = new BclTcpTransportEndpoint(tcp);
        }

        public bool Connected => _inner.Connected;
        public void Send(byte[] data) => _inner.Send(data);
        public bool TryReceive(out byte[] data) => _inner.TryReceive(out data);
        public event Action? OnDisconnected { add => _inner.OnDisconnected += value; remove => _inner.OnDisconnected -= value; }
        public void Dispose() => _inner.Dispose();
    }

    /// <summary>
    /// TCP 端点：包装 TcpClient，后台线程收数据，用 MessageCodec framing。
    /// 内部 Receive 循环读取 → Unframe → 入队；TryReceive 从队列取。
    /// </summary>
    public sealed class BclTcpTransportEndpoint : ITransport
    {
        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly ConcurrentQueue<byte[]> _incoming = new ConcurrentQueue<byte[]>();
        private readonly Thread _receiveThread;
        private volatile bool _connected = true;
        private readonly object _sendLock = new object();

        public BclTcpTransportEndpoint(TcpClient tcp)
        {
            _tcp = tcp;
            _stream = tcp.GetStream();
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "BclTcpTransport.Receive" };
            _receiveThread.Start();
        }

        public bool Connected => _connected;

        public void Send(byte[] data)
        {
            if (!_connected) throw new InvalidOperationException("TCP 传输已断开。");
            lock (_sendLock)
            {
                // 直接写——MessageCodec framing 由调用方（PeerHost/NetworkMatchClient）负责
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
        }

        public bool TryReceive(out byte[] data)
        {
            if (!_connected) { data = Array.Empty<byte>(); return false; }
            if (_incoming.TryDequeue(out byte[]? item))
            {
                data = item!;
                return true;
            }

            data = Array.Empty<byte>();
            return false;
        }

        public event Action? OnDisconnected;

        public void Dispose()
        {
            if (_connected)
            {
                _connected = false;
                try { _tcp.Close(); } catch { }
                OnDisconnected?.Invoke();
            }
        }

        private void ReceiveLoop()
        {
            var buffer = new List<byte>(4096);

            try
            {
                while (_connected)
                {
                    // 先尝试从本地缓冲拆帧
                    while (TryExtractFrame(buffer)) { }

                    // 读一批新数据
                    byte[] chunk = new byte[2048];
                    int n = _stream.Read(chunk, 0, chunk.Length);
                    if (n <= 0) break; // 对端关闭
                    for (int i = 0; i < n; i++) buffer.Add(chunk[i]);
                }
            }
            catch (IOException) when (!_connected) { }
            catch (SocketException) when (!_connected) { }
            catch (ObjectDisposedException) when (!_connected) { }
            finally
            {
                Dispose();
            }
        }

        /// <summary>
        /// 从本地缓冲中尝试拆出一帧：先读 4 字节长度 → 验证 → 读帧体 → 入队 → 从缓冲移除。
        /// 返回 true 表示成功拆出一帧（可以循环调用直到不足）。
        /// </summary>
        private bool TryExtractFrame(List<byte> buffer)
        {
            if (buffer.Count < 4) return false; // 不够读长度前缀

            int bodyLen = BitConverter.ToInt32(buffer.ToArray(), 0);
            if (bodyLen < 5 || bodyLen > MessageCodec.MaxFrameSize)
            {
                // 畸形包——清空缓冲让外层循环断开
                buffer.Clear();
                return false;
            }

            int totalLen = 4 + bodyLen;
            if (buffer.Count < totalLen) return false; // 数据不足

            // 拆出完整帧入队
            byte[] frame = new byte[totalLen];
            buffer.CopyTo(0, frame, 0, totalLen);
            buffer.RemoveRange(0, totalLen);
            _incoming.Enqueue(frame);
            return true;
        }
    }
}
