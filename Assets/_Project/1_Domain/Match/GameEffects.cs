using System;
using System.Collections.Generic;
using System.Globalization;
using Card.Core;

namespace Card.Domain.Match.Effects
{
    /// <summary>
    /// 效果触发时机（Docs/01 §4.3）。T5 实现 OnPlay/OnDeath/OnTurnStart/OnTurnEnd/OnSummon；
    /// 其余（OnAttack/OnAttacked/OnDamageTaken/OnHeal/OnCardDrawn/OnSpellCast）留后续。
    /// </summary>
    public enum Trigger
    {
        OnPlay = 0,
        OnDeath = 1,
        OnTurnStart = 2,
        OnTurnEnd = 3,
        OnSummon = 4
    }

    /// <summary>
    /// 触发时机 + 效果数据的不可变包装（T5）。效果本身（<see cref="Effect"/>）不含时机，
    /// 时机由配置表达式前缀决定（如 <c>OnDeath:DamageEffect:2</c>）。
    /// </summary>
    public sealed class TriggeredEffect
    {
        public TriggeredEffect(Trigger trigger, IEffectData effect)
        {
            Trigger = trigger;
            Effect = Guard.NotNull(effect, nameof(effect));
        }

        public Trigger Trigger { get; }

        public IEffectData Effect { get; }
    }

    /// <summary>
    /// 卡牌效果数据契约（Docs/01 §4.1）：可组合的效果组件，纯数据不含结算逻辑。
    /// 每个 <see cref="IEffectData"/> 实现对应一种效果，执行器在 Application 层按类型分发。
    /// 扩展新效果 = 新增 1 个 IEffectData 实现 + 1 个执行器 + 1 条配置（NFR-5）。
    /// </summary>
    public interface IEffectData
    {
    }

    /// <summary>造成 <see cref="Amount"/> 点伤害（目标来自出牌命令的 Target）。</summary>
    public sealed class DamageEffectData : IEffectData
    {
        public DamageEffectData(int amount)
        {
            Amount = Guard.NotNegative(amount, nameof(amount));
        }

        public int Amount { get; }
    }

    /// <summary>回复 <see cref="Amount"/> 点生命（不超过上限）。</summary>
    public sealed class HealEffectData : IEffectData
    {
        public HealEffectData(int amount)
        {
            Amount = Guard.NotNegative(amount, nameof(amount));
        }

        public int Amount { get; }
    }

    /// <summary>抽 <see cref="Count"/> 张牌（含爆牌与疲劳处理，复用 CardDrawService）。</summary>
    public sealed class DrawCardEffectData : IEffectData
    {
        public DrawCardEffectData(int count)
        {
            Count = Guard.Positive(count, nameof(count));
        }

        public int Count { get; }
    }

    /// <summary>召唤 <see cref="CardKey"/> 随从 <see cref="Count"/> 张到己方战场。</summary>
    public sealed class SummonEffectData : IEffectData
    {
        public SummonEffectData(string cardKey, int count)
        {
            CardKey = Guard.NotNullOrWhiteSpace(cardKey, nameof(cardKey));
            Count = Guard.Positive(count, nameof(count));
        }

        public string CardKey { get; }

        public int Count { get; }
    }

    /// <summary>增益目标随从：攻击力 +<see cref="Attack"/>、生命值 +<see cref="Health"/>（可负）。</summary>
    public sealed class BuffEffectData : IEffectData
    {
        public BuffEffectData(int attack, int health)
        {
            Attack = attack;
            Health = health;
        }

        public int Attack { get; }

        public int Health { get; }
    }

    /// <summary>
    /// 效果配置字符串解析器：把 <see cref="CardDefinition.Effects"/> 的表达式列表
    /// （如 <c>DamageEffect:3</c>、<c>OnDeath:BuffEffect:2,3</c>）解析为带触发时机的效果数据。
    /// 格式 <c>[Trigger:]EffectType:param1,param2</c>；无 Trigger 前缀默认 <see cref="Trigger.OnPlay"/>。
    /// 未知 Trigger/效果类型或格式错误抛 <see cref="ArgumentException"/>。
    /// </summary>
    public static class EffectParser
    {
        private static readonly HashSet<string> KnownTriggers = new HashSet<string>
        {
            "OnPlay", "OnDeath", "OnTurnStart", "OnTurnEnd", "OnSummon"
        };

        public static IReadOnlyList<TriggeredEffect> Parse(IReadOnlyList<string> expressions)
        {
            Guard.NotNull(expressions, nameof(expressions));
            List<TriggeredEffect> result = new List<TriggeredEffect>(expressions.Count);
            for (int i = 0; i < expressions.Count; i++)
            {
                result.Add(ParseOne(expressions[i], i));
            }

            return result;
        }

        private static TriggeredEffect ParseOne(string expression, int index)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new ArgumentException("效果表达式不能为空（索引 " + index + "）。");
            }

            string trimmed = expression.Trim();
            string[] segments = trimmed.Split(':');

            // 判定首段是否为 Trigger：是则剥离，否则默认 OnPlay
            Trigger trigger = Trigger.OnPlay;
            int effectStart = 0;
            if (segments.Length > 1 && KnownTriggers.Contains(segments[0]))
            {
                if (!Enum.TryParse<Trigger>(segments[0], out trigger))
                {
                    throw new ArgumentException("未知触发时机：" + segments[0] + "。");
                }

                effectStart = 1;
            }
            else if (segments.Length > 1 && segments[0].StartsWith("On", StringComparison.Ordinal))
            {
                // 首段以 On 开头但不是已知 Trigger → 配置错误
                throw new ArgumentException("未知触发时机：" + segments[0] + "。");
            }

            string typeName = segments[effectStart];
            string param = effectStart + 1 < segments.Length
                ? string.Join(":", segments, effectStart + 1, segments.Length - effectStart - 1)
                : string.Empty;

            IEffectData effect = ParseEffect(typeName, param);
            return new TriggeredEffect(trigger, effect);
        }

        private static IEffectData ParseEffect(string typeName, string param)
        {
            switch (typeName)
            {
                case "DamageEffect":
                    return new DamageEffectData(ParseInt(param, nameof(DamageEffectData)));
                case "HealEffect":
                    return new HealEffectData(ParseInt(param, nameof(HealEffectData)));
                case "DrawCardEffect":
                    return new DrawCardEffectData(ParseInt(param, nameof(DrawCardEffectData)));
                case "SummonEffect":
                {
                    // 多参数分隔符统一为斜杠：CSV 单元格内逗号需引号转义，斜杠无此负担（Docs/01 §428）。
                    string[] parts = param.Split('/');
                    if (parts.Length != 2)
                    {
                        throw new ArgumentException("SummonEffect 需要 CardKey/Count 两个参数。");
                    }

                    return new SummonEffectData(parts[0].Trim(), ParseInt(parts[1], nameof(SummonEffectData)));
                }
                case "BuffEffect":
                {
                    string[] parts = param.Split('/');
                    if (parts.Length != 2)
                    {
                        throw new ArgumentException("BuffEffect 需要 Attack/Health 两个参数。");
                    }

                    return new BuffEffectData(
                        ParseInt(parts[0], nameof(BuffEffectData)),
                        ParseInt(parts[1], nameof(BuffEffectData)));
                }
                default:
                    throw new ArgumentException("未知效果类型：" + typeName + "。");
            }
        }

        private static int ParseInt(string s, string type)
        {
            if (!int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                throw new ArgumentException(type + " 参数不是有效整数：" + s + "。");
            }

            return v;
        }
    }
}
