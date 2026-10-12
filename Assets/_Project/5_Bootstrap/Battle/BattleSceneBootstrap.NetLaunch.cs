using Card.Application.Launch;
using Card.Bootstrap.Battle.Net;
using Card.Core;
using Card.Domain.Config;
using UnityEngine;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 联机启动参数消费（M14-T4，Docs/02 M14-T4）：从 BattleSceneBootstrap.Net 拆出，
    /// 保证单文件 ≤300 行（铁律 10）。大厅装配 + 启动参数（-host / -join）自动动作；
    /// Docs/05 §15.3"角色切换用启动参数，避免两份构建配置"。
    /// </summary>
    public sealed partial class BattleSceneBootstrap
    {
        /// <summary>大厅装配：建 flow + UI 绑定；返回 launch 供调用方应用自动动作。</summary>
        private (PvpLobbyFlow Flow, NetLaunchAction Launch) CreateNetLobby(
            CardDatabase database, BattleUi ui)
        {
            // 启动参数（-host / -join）指定房主端口时按参数监听，否则用默认端口。
            NetLaunchAction launch = NetLaunch.Resolve(PvpLobbyFlow.DefaultListenPort);
            int listenPort = launch.Kind == NetLaunchKind.Host
                ? launch.Port
                : PvpLobbyFlow.DefaultListenPort;

            var flow = new PvpLobbyFlow(database, listenPort, new UnityTickClock());
            _netLobby = flow;
            _netLobbyUi = PvpLobbyUiFactory.Create(ui.CanvasRoot);
            _netLobbyUi.HostClicked += flow.CreateAsHost;
            _netLobbyUi.JoinClicked += OnNetJoinClicked;
            _netLobbyUi.ReadyClicked += OnNetReadyClicked;
            _netLobbyUi.BackClicked += OnNetBackToMenu;
            flow.StatusChanged += text => OnNetStatus(text);
            _netLobbyUi.SetStatus("选择身份：创建对局（房主）或加入对方房间。");
            _netLobbyUi.SetPhase(PvpLobbyPhase.Choose);
            return (flow, launch);
        }

        /// <summary>启动参数指定角色时自动执行大厅动作（免点击双开）。</summary>
        private void ApplyNetLaunchAction(PvpLobbyFlow flow, NetLaunchAction action)
        {
            switch (action.Kind)
            {
                case NetLaunchKind.Host:
                    GameLog.Info(LogChannel.Boot, $"启动参数：房主自动创建对局（{action}）。");
                    flow.CreateAsHost();
                    break;
                case NetLaunchKind.Join:
                    GameLog.Info(LogChannel.Boot, $"启动参数：自动连接房主（{action}）。");
                    if (_netLobbyUi != null)
                    {
                        _netLobbyUi.AddressInput.text = action.Address; // 输入框与实际连接一致
                    }
                    flow.ConnectAsJoiner(action.Address);
                    break;
            }
        }
    }
}
