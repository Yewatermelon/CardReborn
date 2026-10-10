using System.Collections.Generic;
using Card.Application.Match;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Input;
using Card.Presentation.Battle.Targeting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 热座切换与胜负结算处理器（M6-T3）：
    /// 检测回合切换 → 弹交棒屏 → 视角切换；检测终局 → 胜负面板 → 再来一局。
    /// 纯 Bootstrap 层逻辑，不改规则三层。
    /// </summary>
    internal sealed class HotSeatHandler
    {
        private readonly BattleUi _ui;
        private readonly MatchController _controller;
        private readonly MatchEventPump _pump;
        private readonly BattleViewSynchronizer _synchronizer;
        private readonly PlayerInputController _input;
        private readonly TargetingController _targeting;
        private readonly UiTargetPicker _picker;
        private readonly TableFeedbackLocator _locator;
        private readonly BattleFeedbackPlayer _feedback;
        private int _currentLocalSeat;
        private bool _isAwaitingPass;
        private bool _gameEnded;

        public HotSeatHandler(
            BattleUi ui, MatchController controller, MatchEventPump pump,
            BattleViewSynchronizer synchronizer, PlayerInputController input,
            TargetingController targeting, UiTargetPicker picker,
            TableFeedbackLocator locator, BattleFeedbackPlayer feedback,
            int localSeat)
        {
            _ui = ui;
            _controller = controller;
            _pump = pump;
            _synchronizer = synchronizer;
            _input = input;
            _targeting = targeting;
            _picker = picker;
            _locator = locator;
            _feedback = feedback;
            _currentLocalSeat = localSeat;
        }

        public bool IsBlocked => _isAwaitingPass || _gameEnded;

        /// <summary>绑定交棒屏与胜负面板（PVP 热座全量绑定）。</summary>
        public void BindOverlays()
        {
            if (_ui.PassScreenButton != null)
                _ui.PassScreenButton.onClick.AddListener(OnPassScreenClicked);
            BindVictoryOverlay();
        }

        /// <summary>
        /// 仅绑定胜负面板"再来一局"（PVE 用：无交棒屏）。
        /// 实机冒烟修复：PVE 此前跳过 BindOverlays 导致胜利按钮无监听、点击无反应。
        /// </summary>
        public void BindVictoryOverlay()
        {
            if (_ui.VictoryButton != null)
                _ui.VictoryButton.onClick.AddListener(OnPlayAgainClicked);
        }

        /// <summary>检查终局；如果已终局，pump 完事件并显示胜负面板。</summary>
        public void CheckGameEnd()
        {
            if (_gameEnded || !_controller.IsFinished)
                return;

            _gameEnded = true;
            _pump.Pump();
            _synchronizer.Push(_controller.View);
            ShowVictoryPanel();
        }

        /// <summary>检查回合切换；如果切换了，显示交棒屏并返回 true。</summary>
        public bool CheckTurnChange()
        {
            if (_isAwaitingPass || _gameEnded || _controller.IsFinished)
                return false;

            int activeId = _controller.View.ActivePlayerId;
            if (activeId == _currentLocalSeat)
                return false;

            _isAwaitingPass = true;
            ShowPassScreen(activeId);
            return true;
        }

        private void ShowPassScreen(int newActivePlayer)
        {
            if (_ui.PassScreen == null || _ui.PassScreenLabel == null)
                return;

            _ui.PassScreenLabel.text = "玩家 " + (newActivePlayer + 1) + " 的回合\n点击继续";
            _ui.PassScreen.gameObject.SetActive(true);
        }

        private void OnPassScreenClicked()
        {
            if (!_isAwaitingPass)
                return;

            _isAwaitingPass = false;
            if (_ui.PassScreen != null)
                _ui.PassScreen.gameObject.SetActive(false);

            SwitchPerspective();
            _pump.Pump();
            _synchronizer.Push(_controller.View);
        }

        private void SwitchPerspective()
        {
            _currentLocalSeat = 1 - _currentLocalSeat;
            _synchronizer.SwitchSeats();
            _input._localPlayerId = _currentLocalSeat;
            _targeting._localPlayerId = _currentLocalSeat;
            _picker.SwitchHeroAnchors();
            _locator.SwitchHeroAnchors();
            _feedback.UpdateLocalSeat(_currentLocalSeat);
        }

        private void ShowVictoryPanel()
        {
            if (_ui.VictoryPanel == null || _ui.VictoryLabel == null)
                return;

            int winnerId = -1;
            IReadOnlyList<GameEvent> events = _controller.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i] is MatchEndedEvent me)
                {
                    winnerId = me.WinnerId ?? -1;
                    break;
                }
            }

            _ui.VictoryLabel.text = winnerId switch
            {
                0 => "玩家 1 获胜！",
                1 => "玩家 2 获胜！",
                _ => "平局！",
            };
            _ui.VictoryPanel.gameObject.SetActive(true);
        }

        private void OnPlayAgainClicked()
        {
            SceneManager.LoadScene("Battle");
        }
    }
}
