using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Card.Network
{
    /// <summary>
    /// PeerHost partial：对外监听地址（局域网 IP 发现）。
    /// 独立文件避免主类超 300 行上限（R5）。
    /// </summary>
    public sealed partial class PeerHost
    {
        /// <summary>
        /// 房主对外监听地址（局域网 IP + 端口）。取本机首个非 Loopback 的 IPv4 地址；
        /// 取不到时回退 127.0.0.1（纯本机回环场景）。用于 UI 展示"房间地址"供对手填写。
        /// </summary>
        public string ListenAddress
        {
            get
            {
                string ip = TryGetLanIpv4() ?? "127.0.0.1";
                return ip + ":" + ListenPort;
            }
        }

        private static string? TryGetLanIpv4()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
            catch { /* 取不到就回退 127.0.0.1 */ }
            return null;
        }
    }
}
