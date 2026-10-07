using System;
using Card.Core;
using Card.Domain.Match;
using UnityEngine;

namespace Card.Presentation.Battle.Input
{
    /// <summary>
    /// 玩家输入 → 命令翻译器（M5-T4，铁律 5 / 03 §5.6）：
    /// 把 UI 指针回调（手牌点击/战场点击/技能点击/结束回合）翻译成 <see cref="IGameCommand"/>
    /// 经 <see cref="ICommandSink"/> 提交；自身<strong>不判断任何规则</strong>，
    /// 非法命令由权威侧 <c>RuleEngine</c> 拒绝后经 <see cref="CommandRejected"/> 透出。
    /// 指向性卡牌的目标选择与拖拽箭头属 M5-T5；本类只处理"点击即意图确定"的命令。
    /// </summary>
    public sealed class PlayerInputController : MonoBehaviour
    {
        [SerializeField] internal int _localPlayerId;

        private ICommandSink? _sink;

        /// <summary>命令被权威侧接受时触发（每次提交至多一次）。</summary>
        public event Action? CommandAccepted;

        /// <summary>命令被权威侧拒绝时触发，携带拒绝原因；与 <see cref="CommandAccepted"/> 互斥。</summary>
        public event Action<CommandError>? CommandRejected;

        /// <summary>显式注入命令出口（场景装配期调用一次）；null 抛 <see cref="ArgumentNullException"/>。</summary>
        public void Initialize(ICommandSink sink)
        {
            _sink = Guard.NotNull(sink, nameof(sink));
        }

        /// <summary>手牌点击 → <see cref="PlayCardCommand"/>（目标选择属 M5-T5，此处 Target=None）。</summary>
        public void NotifyHandCardClicked(int cardInstanceId)
        {
            Dispatch(new PlayCardCommand(_localPlayerId, cardInstanceId, TargetRef.None));
        }

        /// <summary>我方随从点击敌方英雄 → <see cref="AttackCommand"/>。</summary>
        public void NotifyBoardMinionClicked(int attackerInstanceId, int targetHeroPlayerId)
        {
            Dispatch(new AttackCommand(_localPlayerId, attackerInstanceId, TargetRef.ForHero(targetHeroPlayerId)));
        }

        /// <summary>英雄技能点击 → <see cref="UseHeroPowerCommand"/>（指向性技能的目标属 M5-T5）。</summary>
        public void NotifyHeroPowerClicked()
        {
            Dispatch(new UseHeroPowerCommand(_localPlayerId, TargetRef.None));
        }

        /// <summary>结束回合按钮 → <see cref="EndTurnCommand"/>。</summary>
        public void NotifyEndTurnClicked()
        {
            Dispatch(new EndTurnCommand(_localPlayerId));
        }

        private void Dispatch(IGameCommand command)
        {
            if (_sink == null)
            {
                throw new InvalidOperationException(
                    "PlayerInputController 未 Initialize（缺少 ICommandSink 装配）。");
            }

            CommandResult result = _sink.Submit(command);
            if (result.IsValid)
            {
                CommandAccepted?.Invoke();
            }
            else
            {
                CommandRejected?.Invoke(result.Error);
            }
        }
    }
}
