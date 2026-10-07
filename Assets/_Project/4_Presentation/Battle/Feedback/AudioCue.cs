namespace Card.Presentation.Battle.Feedback
{
    /// <summary>音效钩子标识（M5-T6）：事件 → 音频 cue 的抽象键，资源绑定属 M6/M9。</summary>
    public enum AudioCue
    {
        CardPlayed,
        AttackDeclared,
        DamageDealt,
        HealingReceived,
        MinionDeath,
        TurnStarted,
        CardDrawn,
        CardBurned,
        FatigueDamage,
        Victory,
        Defeat,
        MatchDraw
    }
}
