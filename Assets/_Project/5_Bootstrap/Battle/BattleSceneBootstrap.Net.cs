using System;
using System.IO;
using Card.Application.Match;
using Card.Bootstrap.Battle.Net;
using Card.Bootstrap.Menu;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Network;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Input;
using Card.Presentation.Battle.Targeting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 联机模式装配（M14-T3，GameMode=PVPNet）：Start 建战斗 UI + 大厅 overlay；
    /// Update 泵大厅 / 对局驱动；开局（MatchStarted + 快照基线）后装配网络战斗管线——
    /// 客户端只收状态（快照+增量 → 视图同步）只上行命令（seq + 在途登记），铁律 12。
    /// 房主与加入方共用同一条装配路径（localSeat 由角色决定：房主 0 / 加入方 1）。
    /// </summary>
    public sealed partial class BattleSceneBootstrap
    {
        public const string PvpNetMode = "PVPNet";

        private PvpLobbyFlow? _netLobby;
        private PvpLobbyUi? _netLobbyUi;
        private NetMatchDriver? _netDriver;
        private MatchEventPump? _netPump;
        private PendingActionFeedback? _netPending;
        private CardDatabase? _netDatabase;
        private ITextResolver? _netTexts;
        private bool _netEndShown;

        private void StartNetMode()
        {
            SceneCamera.Ensure();
            BattleUi ui = BattleUiFactory.Create();
            _ui = ui;

            string configDir = Path.Combine(UnityEngine.Application.streamingAssetsPath, ConfigFolder);
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(configDir);
            if (database == null)
            {
                ShowError(ui, "配置加载失败：\n" + string.Join("\n", errors)
                    + "\n\n请先执行菜单 Card/M6/部署运行时配置。");
                return;
            }

            _netDatabase = database;
            _netTexts = LoadTextResolver(configDir);

            var flow = new PvpLobbyFlow(database, PvpLobbyFlow.DefaultListenPort, new UnityTickClock());
            _netLobby = flow;
            _netLobbyUi = PvpLobbyUiFactory.Create(ui.CanvasRoot);
            _netLobbyUi.HostClicked += flow.CreateAsHost;
            _netLobbyUi.JoinClicked += OnNetJoinClicked;
            _netLobbyUi.ReadyClicked += OnNetReadyClicked;
            _netLobbyUi.BackClicked += OnNetBackToMenu;
            flow.StatusChanged += text => OnNetStatus(text);
            _netLobbyUi.SetStatus("选择身份：创建对局（房主）或加入对方房间。");
            _netLobbyUi.SetPhase(PvpLobbyPhase.Choose);
        }

        private void OnNetJoinClicked()
        {
            if (_netLobbyUi == null || _netLobby == null)
            {
                return;
            }

            _netLobbyUi.RememberAddress();
            _netLobby.ConnectAsJoiner(_netLobbyUi.Address);
        }

        private void OnNetReadyClicked()
        {
            if (_netLobby == null || _netLobbyUi == null)
            {
                return;
            }

            _netLobbyUi.RememberAddress();
            if (_netLobby.IsHosting)
            {
                _netLobby.MarkHostReady();
            }
            else
            {
                _netLobby.SendJoinerReady();
            }
        }

        private void OnNetBackToMenu()
        {
            _netLobby?.Dispose();
            SceneManager.LoadScene(MainMenuBootstrap.BattleSceneName);
        }

        private void OnNetStatus(string text)
        {
            if (_netLobbyUi == null)
            {
                return;
            }

            _netLobbyUi.SetStatus(text);
            if (!text.StartsWith("!", StringComparison.Ordinal)
                && _netLobby is { Driver: not null }
                && !_netLobby.MatchReady)
            {
                _netLobbyUi.SetPhase(PvpLobbyPhase.WaitingReady);
            }
        }

        /// <summary>联机 Update：大厅阶段泵大厅；开局后装配战斗；战斗阶段泵对局驱动。</summary>
        private void UpdateNet()
        {
            if (_netLobby != null)
            {
                _netLobby.Pump();
                if (_netLobby.MatchReady)
                {
                    AssembleNetBattle();
                }

                return;
            }

            if (_netDriver == null || _netPump == null || _synchronizer == null || _ui == null)
            {
                return;
            }

            int applied = _netDriver.Pump();
            bool eventsHandled = _netPump.Pump() > 0;
            if (applied > 0 || eventsHandled)
            {
                if (_netDriver.HasSnapshot)
                {
                    _synchronizer.Push(_netDriver.Sync.State);
                }
            }

            if (_netDriver.IsFinished && !_netEndShown)
            {
                ShowNetEnd();
            }

            _textPool?.ReclaimFinished();
        }

        /// <summary>装配联机战斗管线：视图同步 + 事件泵 + 反馈 + 输入（M14-T1-L1 接线）。</summary>
        private void AssembleNetBattle()
        {
            if (_netLobby == null || _ui == null || _netDatabase == null || _netTexts == null)
            {
                return;
            }

            _netDriver = _netLobby.Driver!;
            int localSeat = _netDriver.LocalSeat;
            int enemySeat = 1 - localSeat;

            _netLobbyUi?.SetPhase(PvpLobbyPhase.Hidden);

            _synchronizer = new BattleViewSynchronizer(_ui, _netDatabase, _netTexts, localSeat, enemySeat);
            _netPump = new MatchEventPump(_netDriver.Events);
            _netPump.EventAppended += _ui.Log.Append;
            BindNetFeedback(localSeat, enemySeat);
            BindNetInput(localSeat, enemySeat);

            _synchronizer.Push(_netDriver.Sync.State);
            GameLog.Info(LogChannel.Boot, "联机对局装配完成（本地座位 " + localSeat + "）。");
        }

        private void BindNetFeedback(int localSeat, int enemySeat)
        {
            if (_ui == null)
            {
                return;
            }

            var locator = new TableFeedbackLocator(
                _ui.LocalBoard, _ui.EnemyBoard,
                localSeat, _ui.LocalHeroAnchor, enemySeat, _ui.EnemyHeroAnchor);
            _locator = locator;
            var settings = new FeedbackSettings();
            _textPool = _ui.FloatingTextPool;
            var feedback = new BattleFeedbackPlayer(
                locator, _ui.FloatingTextPool, _ui.FloatingTextLayer,
                _ui.Banner, new NullAudioCuePlayer(), settings, localSeat);
            _feedback = feedback;
            feedback.Bind(_netPump!);

            var tracker = new PendingCommandTracker();
            CardViewLocator viewLocator = (int instanceId, out CardView? view) =>
                _ui.LocalHand.TryGetCardView(instanceId, out view)
                || _ui.LocalBoard.TryGetCardView(instanceId, out view);
            _netPending = new PendingActionFeedback(tracker, viewLocator, _ui.FloatingTextPool, _ui.FloatingTextLayer);
            _netDriver!.StepApplied += _netPending.NotifyStep;
            _netDriver.PendingCleared += _netPending.ClearPending;
        }

        private void BindNetInput(int localSeat, int enemySeat)
        {
            if (_ui == null)
            {
                return;
            }

            _input = gameObject.AddComponent<PlayerInputController>();
            _input._localPlayerId = localSeat;

            _targeting = gameObject.AddComponent<TargetingController>();
            _targeting._localPlayerId = localSeat;
            _targeting._arrow = _ui.Arrow;

            ICommandSink sink = new PvpCommandSink(_netDriver!, _netPending!);
            _sink = sink;
            _input.Initialize(sink);
            _input.CommandRejected += OnInputRejected;

            var picker = new UiTargetPicker(
                _ui.LocalBoard, _ui.EnemyBoard,
                new (int, RectTransform)[]
                {
                    (localSeat, _ui.LocalHeroAnchor),
                    (enemySeat, _ui.EnemyHeroAnchor),
                });
            _picker = picker;
            _targeting.Initialize(new UnityInputSource(), picker, sink);
            _targeting.CommandRejected += error =>
                GameLog.Warn(LogChannel.Ui, "指向命令被权威侧拒绝：" + error);

            _ui.LocalHand.CardClicked += OnHandCardClicked;
            _ui.LocalBoard.CardClicked += OnBoardMinionClicked;
            _ui.EndTurnButton.onClick.AddListener(OnEndTurnClicked);
            _ui.HeroPowerButton.onClick.AddListener(OnHeroPowerClicked);
        }

        /// <summary>联机终局：胜负面板显示胜者与原因；"再来一局"重载场景回大厅。</summary>
        private void ShowNetEnd()
        {
            if (_ui == null || _netDriver == null)
            {
                return;
            }

            _netEndShown = true;
            if (_ui.VictoryLabel != null)
            {
                _ui.VictoryLabel.text = DescribeNetEnd(_netDriver.WinnerId, _netDriver.EndReason);
            }

            _ui.VictoryPanel?.gameObject.SetActive(true);
            if (_ui.VictoryButton != null)
            {
                _ui.VictoryButton.onClick.AddListener(OnNetPlayAgain);
            }
        }

        private static string DescribeNetEnd(int? winnerId, string? reason)
        {
            string prefix = winnerId switch
            {
                0 => "玩家 1 获胜！",
                1 => "玩家 2 获胜！",
                _ => "对局作废。",
            };

            return reason switch
            {
                PeerHost.EndReasonHostQuit => prefix + "\n（房主已退出）",
                PeerHost.EndReasonDisconnect => prefix + "\n（对手掉线）",
                PeerHost.EndReasonNormal => prefix,
                null => prefix,
                "" => prefix,
                _ => prefix + "\n（" + reason + "）",
            };
        }

        private void OnNetPlayAgain()
        {
            SceneManager.LoadScene("Battle"); // GameMode 仍为 PVPNet → 重载回联机大厅
        }
    }
}
