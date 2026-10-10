using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Domain.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// PVE 人机实盘——AI 回合分帧驱动器（M7-OBS-1）：
    /// 持有 GreedyAiAgent(stepMode:true)，<see cref="Update"/> 里按模拟思考节奏调
    /// <see cref="GreedyAiAgent.StepOne"/> 产出单条命令，StepOne 返回 <c>false</c> 时发 EndTurn 收尾。
    /// 节奏：AI 激活后先"思考" <see cref="InitialThinkSeconds"/> 再首次行动，
    /// 之后每个动作间隔 <see cref="ActionIntervalSeconds"/>——让玩家可观察 AI 操作过程，
    /// "AI 思考中"提示全程可见（实机冒烟反馈：逐帧无延迟时回合瞬间完成，显得突兀）。
    /// 通过 <see cref="AgentMatchRunner.OnSubmitAccepted"/> 拦截默认 Pump，统一由本类调
    /// <see cref="AgentMatchRunner.Pump"/>（Pump 内部 seat 未变时直接 return，安全）。
    /// AI 回合门控 UI 输入（禁用按钮 + 显示思考提示），玩家回合恢复。
    /// </summary>
    public sealed class AiTurnRunner : MonoBehaviour
    {
        private const int AiSeat = 1;

        /// <summary>AI 激活后到首次行动的模拟思考时间（秒）。测试可置 0。</summary>
        public float InitialThinkSeconds = 0.8f;

        /// <summary>AI 两个动作之间的模拟思考间隔（秒）。测试可置 0。</summary>
        public float ActionIntervalSeconds = 0.45f;

        private AgentMatchRunner? _runner;
        private GreedyAiAgent? _ai;
        private MatchController? _controller;
        private BattleUi? _ui;
        private bool _bound;
        private bool _wasAiActive;
        private float _nextActionTime;

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
                _wasAiActive = false;
                SetThinkingVisible(false);
                IsAiTurn = false;
                return;
            }

            bool aiActive = _controller.View.ActivePlayerId == AiSeat;
            IsAiTurn = aiActive;
            SetThinkingVisible(aiActive);
            SetButtonsInteractable(!aiActive);

            if (!aiActive)
            {
                _wasAiActive = false;
                return;
            }

            if (!_wasAiActive)
            {
                // 玩家 → AI 切换瞬间：先"思考"一段再首次行动（延迟≤0 直接放行）。
                _wasAiActive = true;
                _nextActionTime = Time.unscaledTime + InitialThinkSeconds;
                if (InitialThinkSeconds > 0f)
                {
                    return;
                }
            }

            if (Time.unscaledTime < _nextActionTime)
            {
                return;  // 仍在"思考"
            }

            // AI 行动：一次一步；返回 false → 回合结束，发 EndTurn 收尾。
            if (!_ai.StepOne())
            {
                _runner.Submit(new EndTurnCommand(AiSeat));
                // Submit accepted → OnSubmitAccepted → Pump → seat 变 → 激活玩家
            }
            else
            {
                _nextActionTime = Time.unscaledTime + ActionIntervalSeconds;
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
