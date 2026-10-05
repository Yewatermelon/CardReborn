using Card.Core;
using Card.Domain.Config;

namespace Card.Domain.Match
{
    /// <summary>
    /// 卡牌运行时实例（可变；配置 <see cref="CardDefinition"/> 不可变，两者分离，Docs/00 §4.2）。
    /// 随从持有攻击/生命；法术这些值为 0。关键词/状态集合随实例创建而初始化。
    /// </summary>
    public sealed class CardInstance
    {
        private CardInstance(
            int instanceId,
            string cardKey,
            int ownerId,
            int attack,
            int maxHealth,
            int health,
            KeywordSet keywords,
            StatusSet statuses)
        {
            InstanceId = instanceId;
            CardKey = cardKey;
            OwnerId = ownerId;
            Attack = attack;
            MaxHealth = maxHealth;
            Health = health;
            Keywords = keywords;
            Statuses = statuses;
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

        /// <summary>运行时关键词集合（初始值来自配置，可被增益/沉默改变）。</summary>
        public KeywordSet Keywords { get; }

        /// <summary>运行时状态集合（召唤失调/冻结/圣盾）。</summary>
        public StatusSet Statuses { get; }

        /// <summary>从配置定义创建实例：复制初始攻防与关键词，配置变化不影响已生成实例。
        /// 配置含圣盾时，在状态集合中预置可消耗的圣盾状态（关键词=来源，状态=结算实例）。</summary>
        public static CardInstance FromDefinition(
            CardDefinition definition,
            int instanceId,
            int ownerId)
        {
            Guard.NotNull(definition, nameof(definition));
            Guard.NotNegative(instanceId, nameof(instanceId));
            Guard.NotNegative(ownerId, nameof(ownerId));

            KeywordSet keywords = new KeywordSet(definition.Keywords);
            StatusSet statuses = new StatusSet(
                (definition.Keywords & Keyword.DivineShield) == Keyword.DivineShield
                    ? StatusFlags.DivineShield
                    : StatusFlags.None);

            return new CardInstance(
                instanceId,
                definition.Key,
                ownerId,
                definition.Attack,
                definition.Health,
                definition.Health,
                keywords,
                statuses);
        }
    }
}
