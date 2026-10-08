using System;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// 法力水晶（FR-5.2）：<see cref="Max"/> 为永久水晶上限，<see cref="Current"/> 为当前可用。
    /// 规则：每回合上限 +1（受配置 manaLimit 封顶），回合开始回满，未用完不累积。
    /// 业务失败用 <see cref="Result"/> 表达；构造非法属契约错误，直接抛异常。
    /// </summary>
    public sealed class ManaPool : IReadOnlyManaPool
    {
        public const string ErrorInsufficient = "ERROR_MANA_INSUFFICIENT";
        public const string ErrorAmountInvalid = "ERROR_AMOUNT_INVALID";

        public ManaPool(int max = 0, int current = 0)
        {
            Max = Guard.NotNegative(max, nameof(max));
            Current = Guard.NotNegative(current, nameof(current));
        }

        /// <summary>永久水晶上限。</summary>
        public int Max { get; private set; }

        /// <summary>当前可用法力（M3-T5 起可能含临时法力，故构造不强制 ≤ Max）。</summary>
        public int Current { get; private set; }

        /// <summary>是否足以支付 <paramref name="amount"/>。</summary>
        public bool CanSpend(int amount)
        {
            return amount >= 0 && amount <= Current;
        }

        /// <summary>消耗法力；费用为负或不足时返回失败且状态不变。</summary>
        public Result Spend(int amount)
        {
            if (amount < 0)
            {
                return Result.Failure(ErrorAmountInvalid, "费用不能为负数。");
            }

            if (amount > Current)
            {
                return Result.Failure(ErrorInsufficient, "当前法力不足。");
            }

            Current -= amount;
            return Result.Success();
        }

        /// <summary>回合开始：上限 +1 至 <paramref name="manaLimit"/> 封顶，当前法力回满到新上限。</summary>
        public void BeginTurn(int manaLimit)
        {
            Guard.Positive(manaLimit, nameof(manaLimit));

            int grown = Max + 1;
            Max = grown < manaLimit ? grown : manaLimit;
            Current = Max;
        }

        /// <summary>获得临时法力（效果驱动）：直接增加 Current，不改变 Max。</summary>
        public void Gain(int amount)
        {
            Guard.Positive(amount, nameof(amount));
            Current += amount;
        }

        /// <summary>
        /// 快照/增量恢复入口（M4-T10）：直接覆盖 Max/Current，不做 CanSpend 校验。
        /// 负值抛 <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public void Restore(int max, int current)
        {
            if (max < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "恢复后的法力上限不能为负数。");
            }

            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), "恢复后的法力值不能为负数。");
            }

            Max = max;
            Current = current;
        }
    }
}
