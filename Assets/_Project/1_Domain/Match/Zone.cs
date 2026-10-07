using System.Collections.Generic;
using Card.Core;

namespace Card.Domain.Match
{
    /// <summary>
    /// 有序卡牌分区：牌库/坟场容量不限（<c>capacity=null</c>），手牌/战场有上限（来自 RulesConfig）。
    /// 所有失败用 <see cref="Result"/> 表达；<see cref="MoveIn"/> 为原子操作（先全部校验再变更）。
    /// </summary>
    public sealed class Zone : IReadOnlyZone
    {
        public const string ErrorFull = "ERROR_ZONE_FULL";
        public const string ErrorAlreadyPresent = "ERROR_CARD_ALREADY_PRESENT";
        public const string ErrorNotInSource = "ERROR_CARD_NOT_IN_SOURCE";
        public const string ErrorSameZone = "ERROR_SAME_ZONE";

        private readonly List<CardInstance> _cards = new List<CardInstance>();

        public Zone(ZoneType type, int? capacity)
        {
            Type = type;
            Capacity = capacity.HasValue
                ? Guard.NotNegative(capacity.GetValueOrDefault(), nameof(capacity))
                : (int?)null;
        }

        public ZoneType Type { get; }

        /// <summary>容量上限；null 表示不限。</summary>
        public int? Capacity { get; }

        public int Count => _cards.Count;

        public IReadOnlyList<CardInstance> Cards => _cards;

        /// <summary>只读视图：卡牌列表协变为只读卡牌。</summary>
        IReadOnlyList<IReadOnlyCardInstance> IReadOnlyZone.Cards => _cards;

        public bool Contains(CardInstance card) => _cards.Contains(card);

        public bool CanAdd() => !Capacity.HasValue || Count < Capacity.Value;

        /// <summary>追加卡牌并更新其 CurrentZone；重复或满区返回失败。</summary>
        public Result Add(CardInstance card)
        {
            Guard.NotNull(card, nameof(card));

            if (_cards.Contains(card))
            {
                return Result.Failure(ErrorAlreadyPresent, "卡牌已经在该分区中。");
            }

            if (!CanAdd())
            {
                return Result.Failure(ErrorFull, "目标分区已满。");
            }

            _cards.Add(card);
            card.CurrentZone = Type;
            return Result.Success();
        }

        /// <summary>移除卡牌并清空其 CurrentZone；卡牌不在本区返回 false。</summary>
        public bool Remove(CardInstance card)
        {
            Guard.NotNull(card, nameof(card));

            if (!_cards.Remove(card))
            {
                return false;
            }

            card.CurrentZone = null;
            return true;
        }

        /// <summary>把卡牌从 <paramref name="from"/> 移入本区；任一前提不满足则两区都不变。</summary>
        public Result MoveIn(CardInstance card, Zone from)
        {
            Guard.NotNull(card, nameof(card));
            Guard.NotNull(from, nameof(from));

            Result check = ValidateMove(card, from);
            if (check.IsFailure)
            {
                return check;
            }

            from.Remove(card);
            Add(card);
            return Result.Success();
        }

        private Result ValidateMove(CardInstance card, Zone from)
        {
            if (ReferenceEquals(from, this))
            {
                return Result.Failure(ErrorSameZone, "来源分区与目标分区相同。");
            }

            if (_cards.Contains(card))
            {
                return Result.Failure(ErrorAlreadyPresent, "卡牌已经在目标分区中。");
            }

            if (!from.Contains(card))
            {
                return Result.Failure(ErrorNotInSource, "卡牌不在来源分区中。");
            }

            if (!CanAdd())
            {
                return Result.Failure(ErrorFull, "目标分区已满。");
            }

            return Result.Success();
        }
    }
}
