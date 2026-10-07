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

        /// <summary>从配置定义构造显示数据；名称/描述暂以 Key 占位（本地化在 M8 决定）。</summary>
        public static CardViewData FromDefinition(CardDefinition definition)
        {
            Guard.NotNull(definition, nameof(definition));

            return new CardViewData(
                definition.NameKey,
                definition.DescKey,
                definition.Cost,
                definition.Attack,
                definition.Health,
                definition.ArtKey,
                definition.Type);
        }

        /// <summary>
        /// 从局内实例构造显示数据（M5-T2）：攻/血取实例运行时值（受伤/buff 后正确），
        /// 名称/描述/费用/美术/类型仍取自配置。
        /// </summary>
        public static CardViewData FromInstance(CardDefinition definition, CardInstance instance)
        {
            Guard.NotNull(definition, nameof(definition));
            Guard.NotNull(instance, nameof(instance));

            return new CardViewData(
                definition.NameKey,
                definition.DescKey,
                definition.Cost,
                instance.Attack,
                instance.Health,
                definition.ArtKey,
                definition.Type,
                instance.InstanceId);
        }
    }
}
