using System;
using System.Collections.Concurrent;
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
        /// <summary>创建客户端——连接到房主地址。diag 可选注入传输层诊断（M14-T7）。</summary>
        public static BclTcpClient Connect(string host, int port, NetworkDiagnostics? diag = null)
        {
            var client = new TcpClient();
            // M14-T7：禁用 Nagle——心跳 Ping/Pong 是 10 Hz 小包，Nagle + delayed ACK
            // 会滞留小包导致宿主 60 tick 超时误判断链（本机回环交互式对局必须低延迟）。
            client.NoDelay = true;
            client.Connect(host, port);
            return new BclTcpClient(client, diag);
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

        /// <summary>Accept 一条连接（阻塞）；断开或 Dispose 时返回 null。diag 注入传输层诊断（M14-T7）。</summary>
        public BclTcpTransportEndpoint? Accept(NetworkDiagnostics? diag = null)
        {
            try
            {
                TcpClient tcp = _listener.AcceptTcpClient();
                // M14-T7：Accept 侧同样禁用 Nagle（与 Connect 侧对称），原因同上。
                tcp.NoDelay = true;
                return new BclTcpTransportEndpoint(tcp, diag);
            }
            catch (SocketException) when (_disposed)
            {
                return null;
            }
        }

        /// <summary>是否有待接受的连接（非阻塞轮询用，M14-T3）。</summary>
        public bool Pending => !_disposed && _listener.Pending();

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

        public BclTcpClient(TcpClient tcp, NetworkDiagnostics? diag = null)
        {
            _inner = new BclTcpTransportEndpoint(tcp, diag);
        }

        public bool Connected => _inner.Connected;
        public void Send(byte[] data) => _inner.Send(data);
        public bool TryReceive(out byte[] data) => _inner.TryReceive(out data);
        public event Action? OnDisconnected { add => _inner.OnDisconnected += value; remove => _inner.OnDisconnected -= value; }
        public void Dispose() => _inner.Dispose();

        /// <summary>接收线程装配缓冲中的待拆字节数（诊断口，M14-T7）。</summary>
        public int BufferedByteCount => _inner.BufferedByteCount;

        /// <summary>已拆帧待派发的消息数（诊断口，M14-T7）。</summary>
        public int PendingFrameCount => _inner.PendingFrameCount;

        /// <summary>接收线程是否存活（诊断口，M14-T7）。</summary>
        public bool ReceiveThreadAlive => _inner.ReceiveThreadAlive;
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
        private readonly byte[] _recvBuffer = new byte[MessageCodec.MaxFrameSize + 2048];
        private readonly NetworkDiagnostics? _diag;
        private volatile bool _connected = true;
        private volatile bool _receiveThreadAlive;
        private int _recvCount;
        private readonly object _sendLock = new object();

        public BclTcpTransportEndpoint(TcpClient tcp, NetworkDiagnostics? diag = null)
        {
            _tcp = tcp;
            _stream = tcp.GetStream();
            _diag = diag;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "BclTcpTransport.Receive" };
            _receiveThreadAlive = true;
            _receiveThread.Start();
        }

        public bool Connected => _connected;

        /// <summary>已拆帧待派发的消息数（诊断口，M14-T7）。</summary>
        public int PendingFrameCount => _incoming.Count;

        /// <summary>接收线程装配缓冲中的待拆字节数（跨线程读，仅诊断用）。</summary>
        public int BufferedByteCount => _recvCount;

        /// <summary>接收线程是否仍在运行（卡死/退出排查口，M14-T7）。</summary>
        public bool ReceiveThreadAlive => _receiveThreadAlive;

        public void Send(byte[] data)
        {
            if (!_connected) throw new InvalidOperationException("TCP 传输已断开。");
            lock (_sendLock)
            {
                // 直接写——MessageCodec framing 由调用方（PeerHost/NetworkMatchClient）负责。
                // M14-T7：写失败（对端关闭/RST）= 连接已死，按断链处置（与接收循环同口径），
                // 不让 IOException 穿透 PeerSession/NetworkMatchClient 的泵循环。
                try
                {
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
                catch (IOException exception)
                {
                    _diag?.Record(NetworkEventType.SendError, "send-ioex:" + exception.GetType().Name);
                    Dispose();
                }
                catch (SocketException exception)
                {
                    _diag?.Record(NetworkEventType.SendError, "send-sockex:" + exception.SocketErrorCode);
                    Dispose();
                }
                catch (ObjectDisposedException)
                {
                    _diag?.Record(NetworkEventType.SendError, "send-disposed");
                    Dispose();
                }
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
            try
            {
                while (_connected)
                {
                    // 先尝试从装配缓冲拆帧（O(1) 前缀检查 + memmove，零多余拷贝）
                    ExtractFrames();

                    if (_recvCount >= _recvBuffer.Length)
                    {
                        // 缓冲已满仍无完整合法帧——长度前缀已失步，断链而非无限等待
                        NetworkLogger.Warn($"BclTcpTransport 装配缓冲超限断链：buffered={_recvCount}");
                        Dispose();
                        break;
                    }

                    // 直接读进装配缓冲尾部
                    int bytesRead = _stream.Read(_recvBuffer, _recvCount, _recvBuffer.Length - _recvCount);
                    if (bytesRead <= 0) break; // 对端关闭
                    _recvCount += bytesRead;
                    _diag?.Record(NetworkEventType.ReceiveFrame, $"read={bytesRead} buffered={_recvCount}");
                }

                _diag?.Record(NetworkEventType.Disconnected, "recv-eof");
            }
            // 三类 IO 异常都意味着连接不可用（主动关闭或对端断链/RST）：
            // 统一静默走 finally Dispose 断链路径。M12-B2 曾加"when (!_connected)"
            // 意图只吞主动关闭，但 M14-T3 Unity 复跑证明对端 RST 时本端 _connected
            // 仍为 true → 异常逃逸线程顶层 → Unity 记 Exception 日志判测试失败。
            // 接收循环语境下 IO 异常 = 断链，无"需要上抛的真异常"。
            catch (IOException exception)
            {
                _diag?.Record(NetworkEventType.Disconnected, "recv-ioex:" + exception.GetType().Name);
            }
            catch (SocketException exception)
            {
                _diag?.Record(NetworkEventType.Disconnected, "recv-sockex:" + exception.SocketErrorCode);
            }
            catch (ObjectDisposedException)
            {
                _diag?.Record(NetworkEventType.Disconnected, "recv-disposed");
            }
            finally
            {
                _receiveThreadAlive = false;
                Dispose();
            }
        }

        /// <summary>
        /// 从装配缓冲头连续拆帧入队（M14-T7 重写）：原 List&lt;byte&gt; 实现每次拆帧尝试
        /// 都 ToArray 整段拷贝 + 逐字节 Add + RemoveRange 前移，遇到终局大 StateDelta
        /// 连发突发是 O(n²) 拷贝，接收线程可滞后秒级——心跳回包全部迟到，宿主
        /// 60 tick 心跳超时误判断链（M14-T7 Soak 实证）。定长缓冲 + BlockCopy 为 O(n)。
        /// </summary>
        private void ExtractFrames()
        {
            while (_connected && _recvCount >= 4)
            {
                int bodyLen = BitConverter.ToInt32(_recvBuffer, 0);
                if (bodyLen < 5 || bodyLen > MessageCodec.MaxFrameSize)
                {
                    // M14-T7：framing 已坏 = 字节流无法恢复同步，唯一正确终态是断链
                    // （与 PeerSession 畸形帧处置同口径）。
                    NetworkLogger.Warn($"BclTcpTransport 畸形长度前缀断链：bodyLen={bodyLen}");
                    Dispose();
                    return;
                }

                int totalLen = 4 + bodyLen;
                if (_recvCount < totalLen)
                {
                    return; // 数据不足，等下一批读取
                }

                byte[] frame = new byte[totalLen];
                Buffer.BlockCopy(_recvBuffer, 0, frame, 0, totalLen);
                Buffer.BlockCopy(_recvBuffer, totalLen, _recvBuffer, 0, _recvCount - totalLen);
                _recvCount -= totalLen;
                _incoming.Enqueue(frame);
                _diag?.Record(NetworkEventType.ReceiveFrame, $"bytes={totalLen}");
            }
        }
    }
}
