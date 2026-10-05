using Card.Core;
using Card.Domain.Config;

namespace Card.Domain.Match
{
    /// <summary>
    /// 卡牌运行时实例（可变；配置 <see cref="CardDefinition"/> 不可变，两者分离，Docs/00 §4.2）。
    /// 随从持有攻击/生命；法术这些值为 0。关键词与状态在 M3-T3 接入。
    /// </summary>
    public sealed class CardInstance
    {
        private CardInstance(
            int instanceId,
            string cardKey,
            int ownerId,
            int attack,
            int maxHealth,
            int health)
        {
            InstanceId = instanceId;
            CardKey = cardKey;
            OwnerId = ownerId;
            Attack = attack;
            MaxHealth = maxHealth;
            Health = health;
        }

        /// <summary>局内唯一实例 Id（分配器在 M3-T5；此处仅校验非负）。</summary>
        public int InstanceId { get; }

        /// <summary>卡牌定义 Key（指向 Cards 表）。</summary>
        public string CardKey { get; }

        /// <summary>拥有者座位 Id。</summary>
        public int OwnerId { get; }

        public int Attack { get; set; }

        public int MaxHealth { get; }

        public int Health { get; set; }

        /// <summary>当前所在分区；未入任何区为 null。只能由 <see cref="Zone"/> 修改。</summary>
        public ZoneType? CurrentZone { get; internal set; }

        /// <summary>从配置定义创建实例：复制初始攻防，配置变化不影响已生成实例。</summary>
        public static CardInstance FromDefinition(
            CardDefinition definition,
            int instanceId,
            int ownerId)
        {
            Guard.NotNull(definition, nameof(definition));
            Guard.NotNegative(instanceId, nameof(instanceId));
            Guard.NotNegative(ownerId, nameof(ownerId));

            return new CardInstance(
                instanceId,
                definition.Key,
                ownerId,
                definition.Attack,
                definition.Health,
                definition.Health);
        }
    }
}
