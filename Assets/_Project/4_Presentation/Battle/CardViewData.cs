using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// <see cref="ICardViewData"/> 的不可变值实现。
    /// 配置态由 <see cref="FromDefinition"/> 构造；运行时态（buff/伤害后）由 M5-T8 视图模型提供。
    /// </summary>
    public sealed class CardViewData : ICardViewData
    {
        public CardViewData(
            string name,
            string description,
            int cost,
            int attack,
            int health,
            string artKey,
            CardType type,
            int? instanceId = null)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Cost = cost;
            Attack = attack;
            Health = health;
            ArtKey = artKey ?? string.Empty;
            Type = type;
            InstanceId = instanceId;
        }

        public string Name { get; }

        public string Description { get; }

        public int Cost { get; }

        public int Attack { get; }

        public int Health { get; }

        public string ArtKey { get; }

        public CardType Type { get; }

        public int? InstanceId { get; }

        /// <summary>
        /// 从配置定义构造显示数据（M6-T4 起名称/描述经 <paramref name="texts"/> 翻译；
        /// 正式本地化系统在 M8 替换该接缝）。
        /// </summary>
        public static CardViewData FromDefinition(CardDefinition definition, ITextResolver texts)
        {
            Guard.NotNull(definition, nameof(definition));
            Guard.NotNull(texts, nameof(texts));

            return new CardViewData(
                texts.Resolve(definition.NameKey),
                texts.Resolve(definition.DescKey),
                definition.Cost,
                definition.Attack,
                definition.Health,
                definition.ArtKey,
                definition.Type);
        }

        /// <summary>
        /// 从局内实例构造显示数据（M5-T2；M5-T8 起只接受只读视图 <see cref="IReadOnlyCardInstance"/>）：
        /// 攻/血取实例运行时值（受伤/buff 后正确），名称/描述经 <paramref name="texts"/> 翻译，
        /// 费用/美术/类型仍取自配置。
        /// </summary>
        public static CardViewData FromInstance(
            CardDefinition definition, IReadOnlyCardInstance instance, ITextResolver texts)
        {
            Guard.NotNull(definition, nameof(definition));
            Guard.NotNull(instance, nameof(instance));
            Guard.NotNull(texts, nameof(texts));

            return new CardViewData(
                texts.Resolve(definition.NameKey),
                texts.Resolve(definition.DescKey),
                definition.Cost,
                instance.Attack,
                instance.Health,
                definition.ArtKey,
                definition.Type,
                instance.InstanceId);
        }
    }
}
