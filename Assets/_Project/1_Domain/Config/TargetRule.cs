namespace Card.Domain.Config
{
    /// <summary>目标规则（Docs/01 第 4.1 节）：决定出牌是否需要选目标以及可选范围。</summary>
    public enum TargetRule
    {
        None = 0,
        Any = 1,
        Enemy = 2,
        Friendly = 3,
        EnemyMinion = 4,
        FriendlyMinion = 5,
        AnyMinion = 6
    }
}
