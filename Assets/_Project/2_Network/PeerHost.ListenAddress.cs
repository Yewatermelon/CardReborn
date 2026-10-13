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
        /// 房主对外监听地址（局域网 IP + 端口）。取本机最可信的 IPv4 地址
        /// （优先 WiFi → 有效以太网 → 其他），排除回环/隧道/APIPA；
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
                // 候选优先级：WiFi（Wireless80211）> 以太网 > 其他（如虚拟网卡）。
                string? best = null;
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback
                        or NetworkInterfaceType.Tunnel) continue;

                    foreach (UnicastIPAddressInformation info in ni.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress address = info.Address;
                        if (address.AddressFamily != AddressFamily.InterNetwork
                            || IPAddress.IsLoopback(address)) continue;
                        // 排除 APIPA（169.254/16）：链路本地自分配地址（如未插网线的
                        // 有线网卡），本机可达但跨机器不可路由——实测踩坑（M14-T7-B1）。
                        if (address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) continue;

                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        {
                            return address.ToString();
                        }

                        best ??= address.ToString();
                        break; // 同网卡只取第一个有效地址
                    }
                }

                return best;
            }
            catch { /* 取不到就回退 127.0.0.1 */ }
            return null;
        }
    }
}
