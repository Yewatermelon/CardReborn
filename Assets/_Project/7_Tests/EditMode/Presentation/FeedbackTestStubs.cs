using System.Collections.Generic;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>音效钩子录音 stub（M5-T6）：按序记录收到的 cue。</summary>
    internal sealed class StubAudioCuePlayer : IAudioCuePlayer
    {
        public readonly List<AudioCue> Played = new List<AudioCue>();

        public void Play(AudioCue cue)
        {
            Played.Add(cue);
        }
    }

    /// <summary>反馈定位 stub（M5-T6）：可配置命中的锚点坐标与随从视图。</summary>
    internal sealed class StubFeedbackTargetLocator : IFeedbackTargetLocator
    {
        public readonly Dictionary<int, Vector3> MinionAnchors = new Dictionary<int, Vector3>();
        public readonly Dictionary<int, Vector3> HeroAnchors = new Dictionary<int, Vector3>();
        public readonly Dictionary<int, CardView> MinionViews = new Dictionary<int, CardView>();

        public bool TryGetMinionAnchor(int instanceId, out Vector3 worldPosition)
        {
            return MinionAnchors.TryGetValue(instanceId, out worldPosition);
        }

        public bool TryGetHeroAnchor(int seat, out Vector3 worldPosition)
        {
            return HeroAnchors.TryGetValue(seat, out worldPosition);
        }

        public bool TryGetMinionView(int instanceId, out CardView? view)
        {
            bool hit = MinionViews.TryGetValue(instanceId, out CardView found);
            view = found;
            return hit;
        }
    }
}
