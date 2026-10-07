using System;
using System.Globalization;
using Card.Domain.Match;

namespace Card.Presentation.Battle.Log
{
    /// <summary>
    /// 战斗日志格式化器（M5-T7；FR-5.12）。12 类 <see cref="GameEvent"/> 一一映射为单行中文文案。
    /// 文案用座位/实例 Id 占位——卡牌名称本地化属 M8。新增事件派生类型必须在此显式补映射
    /// （未知类型抛 <see cref="ArgumentOutOfRangeException"/>，不容忍静默缺失）。
    /// </summary>
    public static class BattleLogFormatter
    {
        private const string ShieldNote = "（圣盾抵消）";

        public static string Format(GameEvent e)
        {
            switch (e)
            {
                case PhaseChangedEvent p:
                    return $"阶段：{p.From} → {p.To}";
                case TurnStartedEvent t:
                    return $"—— 第 {Inv(t.TurnNumber)} 回合 · 座位 {Inv(t.ActiveSeat)} 行动 ——";
                case TurnEndedEvent t:
                    return $"座位 {Inv(t.ActiveSeat)} 结束回合";
                case CardDrawnEvent d:
                    return $"座位 {Inv(d.Seat)} 抽了一张牌（#{Inv(d.CardInstanceId)}）";
                case CardBurnedEvent b:
                    return $"座位 {Inv(b.Seat)} 手牌已满，爆掉一张牌（#{Inv(b.CardInstanceId)}）";
                case FatigueEvent f:
                    return $"座位 {Inv(f.Seat)} 疲劳受到 {Inv(f.Damage)} 点伤害（第 {Inv(f.FatigueCounter)} 次）";
                case DamageEvent d:
                    return FormatDamage(d);
                case HealingEvent h:
                    return h.TargetInstanceId.HasValue
                        ? $"随从 #{Inv(h.TargetInstanceId.Value)} 恢复 {Inv(h.Amount)} 点生命"
                        : $"英雄（座位 {Inv(h.TargetHeroSeat ?? -1)}）恢复 {Inv(h.Amount)} 点生命";
                case CardDeathEvent d:
                    return $"随从 #{Inv(d.CardInstanceId)} 死亡";
                case CardPlayedEvent p:
                    return $"座位 {Inv(p.Seat)} 打出卡牌 #{Inv(p.CardInstanceId)}";
                case AttackDeclaredEvent a:
                    return a.TargetInstanceId.HasValue
                        ? $"随从 #{Inv(a.AttackerInstanceId)} 攻击随从 #{Inv(a.TargetInstanceId.Value)}"
                        : $"随从 #{Inv(a.AttackerInstanceId)} 攻击英雄（座位 {Inv(a.TargetHeroSeat ?? -1)}）";
                case MatchEndedEvent m:
                    return m.WinnerId.HasValue
                        ? $"对局结束：座位 {Inv(m.WinnerId.Value)} 获胜（第 {Inv(m.TurnNumber)} 回合，{m.Reason}）"
                        : $"对局结束：平局（第 {Inv(m.TurnNumber)} 回合，{m.Reason}）";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(e), e.GetType().Name, "未登记日志映射的事件类型。");
            }
        }

        private static string FormatDamage(DamageEvent d)
        {
            string core = d.TargetInstanceId.HasValue
                ? $"随从 #{Inv(d.TargetInstanceId.Value)} 受到 {Inv(d.Amount)} 点伤害"
                : $"英雄（座位 {Inv(d.TargetHeroSeat ?? -1)}）受到 {Inv(d.Amount)} 点伤害";
            return d.DivineShieldConsumed ? core + ShieldNote : core;
        }

        private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
