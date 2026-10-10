using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Card.Network
{
    /// <summary>
    /// 内存传输（M12-T2）：两个配对端点，可靠有序 FIFO，用于单测（无需真实网络）。
    /// 设计：CreatePair 生成一对互相对端的 ITransport；Send 进对端接收队列，TryReceive 从本端接收队列取。
    /// </summary>
    public static class InMemoryTransport
    {
        public static void CreatePair(out ITransport a, out ITransport b)
        {
            var aToB = new ConcurrentQueue<byte[]>();
            var bToA = new ConcurrentQueue<byte[]>();

            MemoryEndpoint? aEp = null;
            MemoryEndpoint? bEp = null;

            aEp = new MemoryEndpoint(bToA, aToB, () => bEp?.MarkDisconnected());
            bEp = new MemoryEndpoint(aToB, bToA, () => aEp?.MarkDisconnected());

            a = aEp;
            b = bEp;
        }

        private sealed class MemoryEndpoint : ITransport
        {
            private readonly ConcurrentQueue<byte[]> _incoming;
            private readonly ConcurrentQueue<byte[]> _outgoing;
            private readonly Action _peerDisconnectAction;
            private volatile bool _connected = true;

            public MemoryEndpoint(
                ConcurrentQueue<byte[]> incoming,
                ConcurrentQueue<byte[]> outgoing,
                Action peerDisconnectAction)
            {
                _incoming = incoming;
                _outgoing = outgoing;
                _peerDisconnectAction = peerDisconnectAction;
            }

            public bool Connected => _connected;

            public void Send(byte[] data)
            {
                if (!_connected) throw new InvalidOperationException("传输已断开。");
                _outgoing.Enqueue(data);
            }

            public bool TryReceive(out byte[] data)
            {
                if (!_connected)
                {
                    data = Array.Empty<byte>();
                    return false;
                }

                if (_incoming.TryDequeue(out byte[]? item))
                {
                    data = item!;
                    return true;
                }

                data = Array.Empty<byte>();
                return false;
            }

            /// <summary>供对端 Dispose 时回调，标记本端已断开并触发 OnDisconnected。</summary>
            public void MarkDisconnected()
            {
                if (_connected)
                {
                    _connected = false;
                    OnDisconnected?.Invoke();
                }
            }

            public event Action? OnDisconnected;

            public void Dispose()
            {
                MarkDisconnected();
                // 通知对端
                try { _peerDisconnectAction(); } catch { /* 对端可能已 Dispose */ }
            }
        }
    }
}
