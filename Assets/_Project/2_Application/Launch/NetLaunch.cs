using System;
using System.Globalization;
using System.Linq;
using Card.Core;

namespace Card.Application.Launch
{
    /// <summary>启动参数解析出的联机大厅动作类型。</summary>
    public enum NetLaunchKind
    {
        /// <summary>无联机参数：保持现状（主菜单 / 手动选身份）。</summary>
        None = 0,

        /// <summary><c>-host [port]</c>：自动创建对局（房主）。</summary>
        Host = 1,

        /// <summary><c>-join host [port]</c>：自动连接房主。</summary>
        Join = 2
    }

    /// <summary>
    /// 启动参数 → 联机大厅自动动作（M14-T4；Docs/02 M14-T4）。
    /// 语义：<see cref="CommandLineRoleParser"/>（M12-T8）的 Bootstrap 消费层——
    /// 把"角色 + 可选端口"翻译成大厅可直接执行的动作；端口未指定（解析为 0）时
    /// 落到调用方默认端口，保证双开验证地址可复现（Docs/05 §15.3）。
    /// 纯 BCL，kernel 口径可测。
    /// </summary>
    public readonly struct NetLaunchAction
    {
        /// <summary>动作类型。</summary>
        public NetLaunchKind Kind { get; }

        /// <summary>房主监听端口 / 加入端口（已应用默认端口；None 为 0）。</summary>
        public int Port { get; }

        /// <summary>加入目标地址（"host:port"，仅 Join 有效；其余为空串）。</summary>
        public string Address { get; }

        /// <summary>创建动作描述。</summary>
        public NetLaunchAction(NetLaunchKind kind, int port, string address)
        {
            Kind = kind;
            Port = port;
            Address = address;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Kind switch
            {
                NetLaunchKind.Host => $"Host port={Port.ToString(CultureInfo.InvariantCulture)}",
                NetLaunchKind.Join => $"Join {Address}",
                _ => "None"
            };
        }
    }

    /// <summary>
    /// 启动参数解析入口：<c>NetLaunch.Resolve(7777)</c>。
    /// args 传 null 时读取本进程命令行（跳过 exe 路径），与 Unity 构建版 / 编辑器一致。
    /// </summary>
    public static class NetLaunch
    {
        /// <summary>
        /// 解析命令行 → 大厅自动动作。
        /// </summary>
        /// <param name="defaultPort">参数未指定端口时使用（Bootstrap 传 PvpLobbyFlow.DefaultListenPort）。</param>
        /// <param name="args">显式参数（测试注入）；null 读进程命令行。</param>
        public static NetLaunchAction Resolve(int defaultPort, string[]? args = null)
        {
            string[] effective = args ?? Environment.GetCommandLineArgs().Skip(1).ToArray();
            CommandLineRoleParser.ParseResult parsed = CommandLineRoleParser.Parse(effective);
            return parsed.Role switch
            {
                NetworkRole.PeerHost => new NetLaunchAction(
                    NetLaunchKind.Host,
                    parsed.Port > 0 ? parsed.Port : defaultPort,
                    string.Empty),
                NetworkRole.JoinClient => JoinAction(parsed, defaultPort),
                _ => new NetLaunchAction(NetLaunchKind.None, 0, string.Empty)
            };
        }

        private static NetLaunchAction JoinAction(CommandLineRoleParser.ParseResult parsed, int defaultPort)
        {
            int port = parsed.Port > 0 ? parsed.Port : defaultPort;
            string host = parsed.HostAddress ?? string.Empty;
            string address = string.Concat(
                host, ":", port.ToString(CultureInfo.InvariantCulture));
            return new NetLaunchAction(NetLaunchKind.Join, port, address);
        }
    }
}
