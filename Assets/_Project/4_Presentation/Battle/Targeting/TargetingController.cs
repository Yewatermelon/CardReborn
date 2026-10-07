using System;
using Card.Core;
using Card.Domain.Match;
using Card.Presentation.Battle.Input;
using UnityEngine;

namespace Card.Presentation.Battle.Targeting
{
    /// <summary>
    /// 指向状态机（M5-T5，铁律 5 / 03 §5.6）：Idle ↔ Targeting。
    /// 指向中箭头起点经 <c>Func&lt;Vector2&gt;</c> 提供者现算（非快照，布局/分辨率变化仍准确），
    /// 终点跟随指针；左键命中实体 → 构造带 <see cref="TargetRef"/> 的命令经
    /// <see cref="ICommandSink"/> 提交权威侧；右键/Esc 取消。
    /// 自身不读 <see cref="MatchState"/>、不持目标规则、不过滤目标种类——合法性全由 RuleEngine 裁定。
    /// </summary>
    public sealed class TargetingController : MonoBehaviour
    {
        private enum PendingKind
        {
            None = 0,
            PlayCard = 1,
            Attack = 2,
            HeroPower = 3
        }

        [SerializeField] internal int _localPlayerId;
        [SerializeField] internal TargetingArrowView _arrow = null!;

        private IInputSource? _input;
        private ITargetPicker? _picker;
        private ICommandSink? _sink;

        private PendingKind _kind = PendingKind.None;
        private int _pendingId;
        private Func<Vector2>? _origin;

        /// <summary>是否处于指向模式。</summary>
        public bool IsTargeting
        {
            get { return _kind != PendingKind.None; }
        }

        /// <summary>带目标命令被权威侧接受时触发。</summary>
        public event Action? CommandAccepted;

        /// <summary>带目标命令被权威侧拒绝时触发，携带拒绝原因。</summary>
        public event Action<CommandError>? CommandRejected;

        /// <summary>指向被取消（右键/Esc 或被动替换）时触发。</summary>
        public event Action? TargetingCancelled;

        /// <summary>显式注入三类依赖；任一 null 抛 <see cref="ArgumentNullException"/>。</summary>
        public void Initialize(IInputSource input, ITargetPicker picker, ICommandSink sink)
        {
            _input = Guard.NotNull(input, nameof(input));
            _picker = Guard.NotNull(picker, nameof(picker));
            _sink = Guard.NotNull(sink, nameof(sink));
        }

        /// <summary>手牌指向：确认后产出 <see cref="PlayCardCommand"/>。</summary>
        public void BeginPlayCardTargeting(int cardInstanceId, Func<Vector2> originScreenProvider)
        {
            Begin(PendingKind.PlayCard, cardInstanceId, originScreenProvider);
        }

        /// <summary>随从攻击指向：确认后产出 <see cref="AttackCommand"/>。</summary>
        public void BeginAttackTargeting(int attackerInstanceId, Func<Vector2> originScreenProvider)
        {
            Begin(PendingKind.Attack, attackerInstanceId, originScreenProvider);
        }

        /// <summary>英雄技能指向：确认后产出 <see cref="UseHeroPowerCommand"/>。</summary>
        public void BeginHeroPowerTargeting(Func<Vector2> originScreenProvider)
        {
            Begin(PendingKind.HeroPower, 0, originScreenProvider);
        }

        /// <summary>取消当前指向；非指向态为空操作。</summary>
        public void CancelTargeting()
        {
            if (!IsTargeting)
            {
                return;
            }

            ExitTargeting();
            TargetingCancelled?.Invoke();
        }

        private void Update()
        {
            Tick();
        }

        internal void Tick()
        {
            if (!IsTargeting)
            {
                return;
            }

            _arrow.SetEndpoints(_origin!(), _input!.PointerScreenPosition);
            if (_input!.IsCancelPressed)
            {
                CancelTargeting();
                return;
            }

            if (!_input.IsConfirmPressed)
            {
                return;
            }

            if (!_picker!.TryPickTarget(_input.PointerScreenPosition, out TargetRef target))
            {
                return;
            }

            SubmitPending(target);
        }

        private void Begin(PendingKind kind, int pendingId, Func<Vector2> originScreenProvider)
        {
            EnsureInitialized();
            Guard.NotNull(originScreenProvider, nameof(originScreenProvider));
            if (IsTargeting)
            {
                CancelTargeting();
            }

            _kind = kind;
            _pendingId = pendingId;
            _origin = originScreenProvider;
            _arrow.Show();
        }

        private void SubmitPending(TargetRef target)
        {
            IGameCommand command = _kind switch
            {
                PendingKind.PlayCard => new PlayCardCommand(_localPlayerId, _pendingId, target),
                PendingKind.Attack => new AttackCommand(_localPlayerId, _pendingId, target),
                PendingKind.HeroPower => new UseHeroPowerCommand(_localPlayerId, target),
                _ => throw new InvalidOperationException("未知指向模式：" + _kind)
            };

            CommandResult result = _sink!.Submit(command);
            if (result.IsValid)
            {
                CommandAccepted?.Invoke();
            }
            else
            {
                CommandRejected?.Invoke(result.Error);
            }

            ExitTargeting();
        }

        private void ExitTargeting()
        {
            _kind = PendingKind.None;
            _pendingId = 0;
            _origin = null;
            _arrow.Hide();
        }

        private void EnsureInitialized()
        {
            if (_input == null || _picker == null || _sink == null || _arrow == null)
            {
                throw new InvalidOperationException(
                    "TargetingController 未完成装配（Initialize/箭头引用缺失）。");
            }
        }
    }
}
