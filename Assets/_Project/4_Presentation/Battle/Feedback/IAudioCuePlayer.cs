namespace Card.Presentation.Battle.Feedback
{
    /// <summary>音效钩子出口（M5-T6；FR-8.5 钩子部分）：生产实现（AudioSource/混音/音量）属 M6/M9。</summary>
    public interface IAudioCuePlayer
    {
        void Play(AudioCue cue);
    }
}
