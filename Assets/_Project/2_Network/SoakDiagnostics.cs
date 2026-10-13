using System.Text;

namespace Card.Network
{
    /// <summary>
    /// Soak 失败现场诊断拼接（M14-T7）：双端 TCP 接收线程健康 + 双侧诊断事件时间线尾部。
    /// 供 <c>NetworkSoakRunner.DescribeLive</c> 拼进 Failure 文案，定位断链/停摆时刻。
    /// </summary>
    public static class SoakDiagnostics
    {
        /// <summary>尾部事件条数。</summary>
        private const int TailCount = 200;

        /// <summary>
        /// 拼出失败现场后缀（无前导空格）：宿主传输接收线程态 + 客户端传输接收线程态 +
        /// 宿主/客户端诊断环尾部。无 TCP 传输（Loopback 夹具）时省略对应字段。
        /// </summary>
        public static string Describe(
            NetworkMatchClient client, PeerHost peerHost,
            NetworkDiagnostics clientDiag, NetworkDiagnostics hostDiag)
        {
            var sb = new StringBuilder();
            sb.Append(", cliDiag=").Append(Tail(clientDiag));
            sb.Append(", hostDiag=").Append(Tail(hostDiag));

            if (client.Transport is BclTcpClient tcp)
            {
                sb.Append(", tcpAlive=").Append(tcp.ReceiveThreadAlive)
                    .Append(", tcpBuffered=").Append(tcp.BufferedByteCount)
                    .Append(", tcpPending=").Append(tcp.PendingFrameCount);
            }

            if (peerHost.ActiveTransport is BclTcpTransportEndpoint hostTcp)
            {
                sb.Append(", hostTcpAlive=").Append(hostTcp.ReceiveThreadAlive)
                    .Append(", hostTcpBuffered=").Append(hostTcp.BufferedByteCount)
                    .Append(", hostTcpPending=").Append(hostTcp.PendingFrameCount);
            }

            return sb.ToString();
        }

        /// <summary>诊断环最后 N 条（最早→最新），形如 <c>PongReceived@123 PingSent@124(idle=30)</c>。</summary>
        private static string Tail(NetworkDiagnostics diag)
        {
            NetworkEventRecord[] all = diag.Export();
            int start = all.Length > TailCount ? all.Length - TailCount : 0;
            var sb = new StringBuilder("[");
            for (int i = start; i < all.Length; i++)
            {
                if (i > start)
                {
                    sb.Append(' ');
                }

                sb.Append(all[i].EventType).Append('@').Append(all[i].Tick);
                if (all[i].Detail.Length > 0)
                {
                    sb.Append('(').Append(all[i].Detail).Append(')');
                }
            }

            return sb.Append(']').ToString();
        }
    }
}
