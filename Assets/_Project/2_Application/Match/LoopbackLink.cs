using System.Collections.Generic;

namespace Card.Application.Match
{
    /// <summary>
    /// 进程内回环链路（M4-T10）：一对互通的单向 FIFO 端点，供"权威服务器 ↔ 只读客户端"
    /// 同步消息。无任何真实网络（无 Socket/线程/异步），单线程 Pump 驱动。
    /// </summary>
    public static class LoopbackLink
    {
        /// <summary>创建一对互通端点：a 发的 b 收，b 发的 a 收。</summary>
        public static void CreatePair(out LoopbackEndpoint a, out LoopbackEndpoint b)
        {
            Queue<string> aToB = new Queue<string>();
            Queue<string> bToA = new Queue<string>();
            a = new LoopbackEndpoint(aToB, bToA);
            b = new LoopbackEndpoint(bToA, aToB);
        }
    }

    /// <summary>单向 FIFO 端点：Send 进对端接收队列，TryReceive 从本端接收队列取。</summary>
    public sealed class LoopbackEndpoint
    {
        private readonly Queue<string> _outgoing;
        private readonly Queue<string> _incoming;

        internal LoopbackEndpoint(Queue<string> outgoing, Queue<string> incoming)
        {
            _outgoing = outgoing;
            _incoming = incoming;
        }

        /// <summary>本端待收消息数。</summary>
        public int Pending => _incoming.Count;

        /// <summary>向对端发送一条消息（FIFO）。</summary>
        public void Send(string message)
        {
            _outgoing.Enqueue(message);
        }

        /// <summary>取一条本端待收消息；无消息返回 false。</summary>
        public bool TryReceive(out string message)
        {
            if (_incoming.Count == 0)
            {
                message = string.Empty;
                return false;
            }

            message = _incoming.Dequeue();
            return true;
        }
    }
}
