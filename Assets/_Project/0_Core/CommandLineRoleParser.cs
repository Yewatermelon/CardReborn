using System;
using System.Linq;

namespace Card.Core
{
    /// <summary>
    /// 联网角色枚举（M12-T8）：决定本次启动以房主 / 加入者 / 热座模式运行。
    /// 放 <c>Card.Core</c> 是因为纯 BCL，kernel 工具链可编译、可测。
    /// </summary>
    public enum NetworkRole
    {
        /// <summary>默认：本地热座（同机两玩家轮流操作）。</summary>
        HotSeat = 0,

        /// <summary>本机房主：启动 PeerHost 监听，玩家 1（房主）在此端。</summary>
        PeerHost = 1,

        /// <summary>加入者：连接到房主地址，玩家 2 在此端。</summary>
        JoinClient = 2
    }

    /// <summary>
    /// 启动参数解析器（M12-T8；Docs/02 M12-T8）：
    /// <c>-host [port]</c> → PeerHost（可选指定监听端口，默认 OS 分配）
    /// <c>-join host [port]</c> → JoinClient（连接到指定地址）
    /// 无参数 → HotSeat（本地热座，默认）
    ///
    /// 本类是 BCL 纯代码，kernel 工具链可编译、可测。放 <c>Card.Core</c> 是为了让 kernel 能引用。
    /// </summary>
    public static class CommandLineRoleParser
    {
        /// <summary>解析结果。</summary>
        public sealed class ParseResult
        {
            public NetworkRole Role { get; init; }
            public string? HostAddress { get; init; }
            public int Port { get; init; }
            public string[] Args { get; init; } = Array.Empty<string>();

            public override string ToString()
            {
                return Role switch
                {
                    NetworkRole.PeerHost => $"PeerHost port={Port}",
                    NetworkRole.JoinClient => $"JoinClient {HostAddress}:{Port}",
                    _ => "HotSeat (default)"
                };
            }
        }

        /// <summary>
        /// 从命令行参数解析角色；传入 null 时使用 <see cref="Environment.GetCommandLineArgs"/>。
        /// </summary>
        public static ParseResult Parse(string[]? args = null)
        {
            string[] effective = args ?? Environment.GetCommandLineArgs().Skip(1).ToArray();

            if (effective.Length == 0)
            {
                return new ParseResult { Role = NetworkRole.HotSeat, Args = effective };
            }

            string first = effective[0].TrimStart('-');
            // 同时支持 -host=9090 形式
            int eqIdx = first.IndexOf('=');
            string key = eqIdx >= 0 ? first.Substring(0, eqIdx) : first;
            string? inlinePort = eqIdx >= 0 ? first.Substring(eqIdx + 1) : null;

            switch (key)
            {
                case "host":
                {
                    int port = 0;
                    if (inlinePort != null && int.TryParse(inlinePort, out int ip))
                    {
                        port = ip;
                    }
                    else if (effective.Length > 1 && int.TryParse(effective[1], out int p))
                    {
                        port = p;
                    }
                    return new ParseResult { Role = NetworkRole.PeerHost, Port = port, Args = effective };
                }

                case "join":
                {
                    // -join host [port] 或 -join=host:port
                    string host;
                    int port = 0;

                    if (inlinePort != null)
                    {
                        // -join=host:port 形式
                        int colon = inlinePort.LastIndexOf(':');
                        if (colon >= 0)
                        {
                            host = inlinePort.Substring(0, colon);
                            int.TryParse(inlinePort.Substring(colon + 1), out port);
                        }
                        else
                        {
                            host = inlinePort;
                        }
                    }
                    else
                    {
                        if (effective.Length < 2)
                        {
                            throw new ArgumentException("-join 需要指定主机地址。用法：-join host [port]");
                        }

                        host = effective[1];
                        if (effective.Length > 2 && int.TryParse(effective[2], out int p))
                        {
                            port = p;
                        }
                    }

                    return new ParseResult { Role = NetworkRole.JoinClient, HostAddress = host, Port = port, Args = effective };
                }

                default:
                    // 不识别的参数——回退 HotSeat（不崩溃）
                    return new ParseResult { Role = NetworkRole.HotSeat, Args = effective };
            }
        }
    }
}
