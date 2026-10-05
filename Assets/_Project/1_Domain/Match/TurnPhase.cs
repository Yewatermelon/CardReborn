namespace Card.Domain.Match
{
    /// <summary>对局回合阶段（Docs/01 §3.2）。阶段流转校验在 M3-T6 / M4，此处仅为数据枚举。</summary>
    public enum TurnPhase
    {
        MatchStart = 0,
        TurnStart = 1,
        Draw = 2,
        Main = 3,
        TurnEnd = 4,
        MatchEnd = 5
    }
}
