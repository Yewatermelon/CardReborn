namespace Card.Domain.Match
{
    /// <summary>对局结果枚举（M3-T7）。</summary>
    public enum MatchResult
    {
        /// <summary>对局进行中。</summary>
        Ongoing,

        /// <summary>座 0 获胜。</summary>
        Player0Wins,

        /// <summary>座 1 获胜。</summary>
        Player1Wins,

        /// <summary>平局（双方同时归零）。</summary>
        Draw
    }
}
