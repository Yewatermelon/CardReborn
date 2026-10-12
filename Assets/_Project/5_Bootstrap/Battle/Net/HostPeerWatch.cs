using Card.Core;
using Card.Network;

namespace Card.Bootstrap.Battle.Net
{
    /// <summary>
    /// 房主侧对手掉线/重连监视（M14-T3-B5）：把 <see cref="PeerHost"/> 的会话状态翻译为
    /// 战斗日志提示——掉线即提示等待重连（权威侧重连窗口约 30 秒后自动判负，PeerHost
    /// RunDisconnectForfeit），重连即提示恢复。仅房主流程装配（加入方 HostPeer 为 null）。
    /// </summary>
    internal sealed class HostPeerWatch
    {
        private readonly PeerHost _host;
        private readonly BattleUi _ui;
        private bool _downShown;

        private HostPeerWatch(PeerHost host, BattleUi ui)
        {
            _host = host;
            _ui = ui;
        }

        /// <summary>房主流程创建；非房主（host 为 null）返回 null。</summary>
        public static HostPeerWatch? TryCreate(PeerHost? host, BattleUi ui)
        {
            return host == null ? null : new HostPeerWatch(host, Guard.NotNull(ui, nameof(ui)));
        }

        /// <summary>每帧调用：会话断开/恢复各提示一次（状态翻转驱动，不刷屏）。</summary>
        public void Pump()
        {
            if (!_host.MatchLive || _host.LastMatchEndReason != null)
            {
                return;
            }

            PeerSession? session = _host.ActiveSession;
            bool down = session == null || !session.IsConnected;
            if (down && !_downShown)
            {
                _downShown = true;
                _ui.Log.AppendText("!对手已断线，等待重连（约 30 秒后判负）…");
                GameLog.Warn(LogChannel.Network, "房主侧对手掉线，重连等待窗口开启。");
            }
            else if (!down && _downShown)
            {
                _downShown = false;
                _ui.Log.AppendText("对手已重连，对局继续。");
                GameLog.Info(LogChannel.Network, "房主侧对手重连完成。");
            }
        }
    }
}
