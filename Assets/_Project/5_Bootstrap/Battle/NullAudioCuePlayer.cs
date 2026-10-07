using Card.Presentation.Battle.Feedback;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 空音效播放器（M6-T1）：M5-T6 <see cref="IAudioCuePlayer"/> 的占位实现，
    /// 事件钩子照常触发但无声；真实 AudioClip 与混音属 M9。
    /// </summary>
    public sealed class NullAudioCuePlayer : IAudioCuePlayer
    {
        public void Play(AudioCue cue)
        {
            // 故意为空：T1 阶段无音频资源。
        }
    }
}
