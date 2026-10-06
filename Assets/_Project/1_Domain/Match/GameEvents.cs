using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// 对局状态变更事件基类（M4-T3；FR-13.7）。所有影响表现的状态变化都产出派生事件，
    /// 事件顺序与结算顺序一致。<see cref="Sequence"/> 由 <see cref="EventLog"/> 在
    /// emit 时分配（单调从 0 起），生产者不关心。
    ///
    /// 本类型与 <c>MatchStepRecord</c>（命令处理台账）职责不同：台账记录"命令是否接受"，
    /// 事件记录"状态如何变化"。事件可被 JSON 序列化（无循环引用，字段为基础类型/枚举），
    /// 序列化器在 M4-T9 实现。
    /// </summary>
    public abstract class GameEvent
    {
        /// <summary>事件在整局事件流中的序号，由 EventLog 分配（0 起）。</summary>
        public int Sequence { get; internal set; }
    }

    /// <summary>回合阶段从 <see cref="From"/> 流转到 <see cref="To"/>。</summary>
    public sealed class PhaseChangedEvent : GameEvent
    {
        public PhaseChangedEvent(TurnPhase from, TurnPhase to)
        {
            From = from;
            To = to;
        }

        public TurnPhase From { get; }
        public TurnPhase To { get; }
    }

    /// <summary>新回合开始：<see cref="ActiveSeat"/> 获得行动权。</summary>
    public sealed class TurnStartedEvent : GameEvent
    {
        public TurnStartedEvent(int turnNumber, int activeSeat)
        {
            TurnNumber = turnNumber;
            ActiveSeat = activeSeat;
        }

        public int TurnNumber { get; }
        public int ActiveSeat { get; }
    }

    /// <summary>当前行动方的回合结束。</summary>
    public sealed class TurnEndedEvent : GameEvent
    {
        public TurnEndedEvent(int turnNumber, int activeSeat)
        {
            TurnNumber = turnNumber;
            ActiveSeat = activeSeat;
        }

        public int TurnNumber { get; }
        public int ActiveSeat { get; }
    }

    /// <summary>玩家从牌库抽到一张牌入手牌。</summary>
    public sealed class CardDrawnEvent : GameEvent
    {
        public CardDrawnEvent(int seat, int cardInstanceId)
        {
            Seat = seat;
            CardInstanceId = cardInstanceId;
        }

        public int Seat { get; }
        public int CardInstanceId { get; }
    }

    /// <summary>手牌已满，一张牌被爆掉（移出牌库入坟场，不入手牌）。</summary>
    public sealed class CardBurnedEvent : GameEvent
    {
        public CardBurnedEvent(int seat, int cardInstanceId)
        {
            Seat = seat;
            CardInstanceId = cardInstanceId;
        }

        public int Seat { get; }
        public int CardInstanceId { get; }
    }

    /// <summary>牌库为空时抽牌受到的递增疲劳伤害。</summary>
    public sealed class FatigueEvent : GameEvent
    {
        public FatigueEvent(int seat, int damage, int fatigueCounter)
        {
            Seat = seat;
            Damage = damage;
            FatigueCounter = fatigueCounter;
        }

        public int Seat { get; }
        public int Damage { get; }
        public int FatigueCounter { get; }
    }

    /// <summary>一次伤害结算（生产者 M4-T6 CombatResolver / M4-T4 法术）。</summary>
    public sealed class DamageEvent : GameEvent
    {
        public DamageEvent(
            int? sourceInstanceId,
            int? targetInstanceId,
            int? targetHeroSeat,
            int amount,
            bool divineShieldConsumed)
        {
            SourceInstanceId = sourceInstanceId;
            TargetInstanceId = targetInstanceId;
            TargetHeroSeat = targetHeroSeat;
            Amount = amount;
            DivineShieldConsumed = divineShieldConsumed;
        }

        public int? SourceInstanceId { get; }
        public int? TargetInstanceId { get; }
        public int? TargetHeroSeat { get; }
        public int Amount { get; }
        public bool DivineShieldConsumed { get; }
    }

    /// <summary>一次治疗结算（生产者 M4-T4 效果框架）。</summary>
    public sealed class HealingEvent : GameEvent
    {
        public HealingEvent(int? targetInstanceId, int? targetHeroSeat, int amount)
        {
            TargetInstanceId = targetInstanceId;
            TargetHeroSeat = targetHeroSeat;
            Amount = amount;
        }

        public int? TargetInstanceId { get; }
        public int? TargetHeroSeat { get; }
        public int Amount { get; }
    }

    /// <summary>随从生命 ≤ 0 进入死亡结算（生产者 M4-T7）。</summary>
    public sealed class CardDeathEvent : GameEvent
    {
        public CardDeathEvent(int cardInstanceId)
        {
            CardInstanceId = cardInstanceId;
        }

        public int CardInstanceId { get; }
    }

    /// <summary>玩家打出一张手牌（生产者 M4-T4）。</summary>
    public sealed class CardPlayedEvent : GameEvent
    {
        public CardPlayedEvent(int seat, int cardInstanceId)
        {
            Seat = seat;
            CardInstanceId = cardInstanceId;
        }

        public int Seat { get; }
        public int CardInstanceId { get; }
    }

    /// <summary>随从发起攻击（生产者 M4-T6）。</summary>
    public sealed class AttackDeclaredEvent : GameEvent
    {
        public AttackDeclaredEvent(int attackerInstanceId, int? targetInstanceId, int? targetHeroSeat)
        {
            AttackerInstanceId = attackerInstanceId;
            TargetInstanceId = targetInstanceId;
            TargetHeroSeat = targetHeroSeat;
        }

        public int AttackerInstanceId { get; }
        public int? TargetInstanceId { get; }
        public int? TargetHeroSeat { get; }
    }

    /// <summary>对局终局（由控制器终局判定单入口产出）。</summary>
    public sealed class MatchEndedEvent : GameEvent
    {
        public MatchEndedEvent(MatchResult result, int? winnerId, int turnNumber, string reason)
        {
            Result = result;
            WinnerId = winnerId;
            TurnNumber = turnNumber;
            Reason = reason;
        }

        public MatchResult Result { get; }
        public int? WinnerId { get; }
        public int TurnNumber { get; }
        public string Reason { get; }
    }

    /// <summary>事件接收器：结算器/控制器向其产出事件。</summary>
    public interface IEventSink
    {
        void Emit(GameEvent e);
    }

    /// <summary>
    /// 事件日志：按 emit 顺序追加事件并分配单调 <see cref="GameEvent.Sequence"/>。
    /// 单局一个实例，权威侧持有；表现层与状态下发（M13）只读消费。
    /// </summary>
    public sealed class EventLog : IEventSink
    {
        private readonly List<GameEvent> _events = new List<GameEvent>();

        public void Emit(GameEvent e)
        {
            Guard.NotNull(e, nameof(e));
            e.Sequence = _events.Count;
            _events.Add(e);
        }

        public IReadOnlyList<GameEvent> Events => _events;

        public int Count => _events.Count;

        public GameEvent? Last => _events.Count > 0 ? _events[_events.Count - 1] : null;
    }
}
