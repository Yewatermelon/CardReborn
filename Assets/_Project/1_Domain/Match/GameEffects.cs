using System;
using System.Collections.Generic;
using System.Globalization;
using Card.Core;

namespace Card.Domain.Match.Effects
{
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
    /// （如 <c>DamageEffect:3</c>、<c>BuffEffect:2,3</c>）解析为效果数据对象。
    /// 格式 <c>TypeName:param1,param2</c>；未知类型或格式错误抛 <see cref="ArgumentException"/>。
    /// </summary>
    public static class EffectParser
    {
        public static IReadOnlyList<IEffectData> Parse(IReadOnlyList<string> expressions)
        {
            Guard.NotNull(expressions, nameof(expressions));
            List<IEffectData> result = new List<IEffectData>(expressions.Count);
            for (int i = 0; i < expressions.Count; i++)
            {
                result.Add(ParseOne(expressions[i], i));
            }

            return result;
        }

        private static IEffectData ParseOne(string expression, int index)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new ArgumentException("效果表达式不能为空（索引 " + index + "）。");
            }

            string trimmed = expression.Trim();
            int colon = trimmed.IndexOf(':');
            string typeName = colon < 0 ? trimmed : trimmed.Substring(0, colon);
            string param = colon < 0 ? string.Empty : trimmed.Substring(colon + 1);

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
                    string[] parts = param.Split(',');
                    if (parts.Length != 2)
                    {
                        throw new ArgumentException("SummonEffect 需要 CardKey,Count 两个参数。");
                    }

                    return new SummonEffectData(parts[0].Trim(), ParseInt(parts[1], nameof(SummonEffectData)));
                }
                case "BuffEffect":
                {
                    string[] parts = param.Split(',');
                    if (parts.Length != 2)
                    {
                        throw new ArgumentException("BuffEffect 需要 Attack,Health 两个参数。");
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
