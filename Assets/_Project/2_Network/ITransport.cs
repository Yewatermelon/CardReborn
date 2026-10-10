using System;

namespace Card.Network
{
    /// <summary>
    /// 传输层抽象（M12-T2）：可靠有序的字节流通道。
    /// <see cref="InMemoryTransport"/> 用于单测（无需真实网络），
    /// <see cref="BclTcpTransport"/> 用于本机双进程直连（BCL System.Net.Sockets）。
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>连接是否仍然有效。</summary>
        bool Connected { get; }

        /// <summary>将字节数组完整发送到对端。</summary>
        void Send(byte[] data);

        /// <summary>尝试读取一段数据；无数据时返回 false，对端关闭时也返回 false（Connected 会变为 false）。</summary>
        bool TryReceive(out byte[] data);

        /// <summary>对端断开时触发（仅一次）。</summary>
        event Action? OnDisconnected;
    }
}
