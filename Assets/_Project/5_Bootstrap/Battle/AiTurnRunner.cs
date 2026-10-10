using Card.Application.Match.Agents;
using Card.Domain.Match;
using Card.Presentation.Battle.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// PVE 人机实盘——AI 回合分帧驱动器（M7-OBS-1）：
    /// 持有 GreedyAiAgent(stepMode:true)，<see cref="Update"/> 里每帧调 <see cref="GreedyAiAgent.StepOne"/>
    /// 产出单条命令，StepOne 返回 <c>false</c> 时发 EndTurn 收尾。
    /// 通过 <see cref="AgentMatchRunner.OnSubmitAccepted"/> 拦截默认 Pump，统一由本类调
    /// <see cref="AgentMatchRunner.Pump"/>（Pump 内部 seat 未变时直接 return，安全）。
    /// AI 回合门控 UI 输入（禁用按钮 + 显示思考提示），玩家回合恢复。
    /// </summary>
    public sealed class AiTurnRunner : MonoBehaviour
    {
        private const int AiSeat = 1;

        private AgentMatchRunner? _runner;
        private GreedyAiAgent? _ai;
        private MatchController? _controller;
        private BattleUi? _ui;
        private bool _bound;

        /// <summary>AI 回合为 <c>true</c>，用于 BattleSceneBootstrap 输入回调门控。</summary>
        public bool IsAiTurn { get; private set; }

        /// <summary>
        /// 装配：保存引用、绑定 runner.OnSubmitAccepted（拦截默认 Pump）。
        /// 由 BattleSceneBootstrap 在 Start 里调用一次。
        /// </summary>
        public void Bind(AgentMatchRunner runner, GreedyAiAgent ai, MatchController controller, BattleUi ui)
        {
            _runner = runner;
            _ai = ai;
            _controller = controller;
            _ui = ui;
            _bound = true;

            // 拦截默认 Pump：总是调 _runner.Pump()，Pump 内部 seat 未变时安全 return。
            runner.OnSubmitAccepted = OnSubmitAccepted;
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.OnSubmitAccepted = null;  // 恢复默认行为
            }
        }

        private void OnSubmitAccepted()
        {
            if (_controller != null && _controller.IsFinished)
                return;

            _runner?.Pump();
        }

        private void Update()
        {
            if (!_bound || _controller == null || _runner == null || _ai == null)
                return;

            if (_controller.IsFinished)
            {
                SetThinkingVisible(false);
                IsAiTurn = false;
                return;
            }

            bool aiActive = _controller.View.ActivePlayerId == AiSeat;
            IsAiTurn = aiActive;
            SetThinkingVisible(aiActive);
            SetButtonsInteractable(!aiActive);

            if (!aiActive)
                return;

            // AI 回合：每帧 StepOne；返回 false → 回合结束，发 EndTurn 收尾。
            if (!_ai.StepOne())
            {
                _runner.Submit(new EndTurnCommand(AiSeat));
                // Submit accepted → OnSubmitAccepted → Pump → seat 变 → 激活玩家
            }
        }

        private void SetThinkingVisible(bool visible)
        {
            if (_ui?.ThinkingLabel != null)
            {
                _ui.ThinkingLabel.gameObject.SetActive(visible);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_ui?.EndTurnButton != null)
                _ui.EndTurnButton.interactable = interactable;
            if (_ui?.HeroPowerButton != null)
                _ui.HeroPowerButton.interactable = interactable;
        }
    }
}
