using Card.Core;
using Card.Network;
using UnityEngine.SceneManagement;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// M14-T6 联机终局异常体验：DescribeNetEnd 原因翻译 + ShowNetEnd 胜负面板 +
    /// OnNetReturnToMain 清理网络后返回主菜单。
    /// </summary>
    internal partial class BattleSceneBootstrap
    {
        /// <summary>联机终局：胜负面板显示胜者与原因；"再来一局" + "返回主菜单"。</summary>
        private void ShowNetEnd(string? overrideText = null)
        {
            if (_ui == null || _netDriver == null) { return; }
            _netEndShown = true;
            if (_ui.VictoryLabel != null)
            {
                _ui.VictoryLabel.text = overrideText ?? DescribeNetEnd(_netDriver.WinnerId, _netDriver.EndReason);
            }

            _ui.VictoryPanel?.gameObject.SetActive(true);
            if (!_netEndButtonsBound)
            {
                _netEndButtonsBound = true;
                if (_ui.VictoryButton != null) { _ui.VictoryButton.onClick.AddListener(OnNetPlayAgain); }
                if (_ui.VictorySecondaryButton != null) { _ui.VictorySecondaryButton.onClick.AddListener(OnNetReturnToMain); }
            }
        }

        private static string DescribeNetEnd(int? winnerId, string? reason)
        {
            string prefix = winnerId switch { 0 => "玩家 1 获胜！", 1 => "玩家 2 获胜！", _ => "对局作废。" };
            return reason switch
            {
                PeerHost.EndReasonHostQuit => prefix + "\n（房主已退出）",
                PeerHost.EndReasonDisconnect => prefix + "\n（对手掉线）",
                null or "" => prefix,
                _ => prefix + "\n（" + reason + "）",
            };
        }

        private void OnNetPlayAgain() => SceneManager.LoadScene("Battle");

        private void OnNetReturnToMain()
        {
            _netLobby?.Dispose();
            _netDriver?.Dispose();
            _netPeerWatch = null;
            SceneManager.LoadScene(MainMenuBootstrap.MainMenuSceneName);
        }
    }
}
