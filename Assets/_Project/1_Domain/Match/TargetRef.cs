using System;

namespace Card.Domain.Match
{
    /// <summary>目标的种类：无目标 / 随从 / 英雄。</summary>
    public enum TargetKind
    {
        None = 0,
        Minion = 1,
        Hero = 2
    }

    /// <summary>
    /// 命令对目标角色的引用（不可变纯数据，M3-T4）：随从按
    /// <see cref="CardInstance.InstanceId"/>、英雄按玩家座位 Id；无目标为 <see cref="None"/>。
    /// 不持有对象引用，可序列化、可在权威侧按 Id 查询。
    /// </summary>
    public readonly struct TargetRef : IEquatable<TargetRef>
    {
        private TargetRef(TargetKind kind, int targetId)
        {
            Kind = kind;
            TargetId = targetId;
        }

        public TargetKind Kind { get; }

        /// <summary>目标的稳定 Id：随从 = InstanceId，英雄 = 座位 Id，None = 0。</summary>
        public int TargetId { get; }

        public bool IsNone
        {
            get { return Kind == TargetKind.None; }
        }

        public static TargetRef None
        {
            get { return new TargetRef(TargetKind.None, 0); }
        }

        public static TargetRef ForMinion(int instanceId)
        {
            return new TargetRef(TargetKind.Minion, instanceId);
        }

        public static TargetRef ForHero(int playerId)
        {
            return new TargetRef(TargetKind.Hero, playerId);
        }

        public bool Equals(TargetRef other)
        {
            return Kind == other.Kind && TargetId == other.TargetId;
        }

        public override bool Equals(object? obj)
        {
            return obj is TargetRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ TargetId;
            }
        }

        public static bool operator ==(TargetRef left, TargetRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TargetRef left, TargetRef right)
        {
            return !left.Equals(right);
        }
    }
}
