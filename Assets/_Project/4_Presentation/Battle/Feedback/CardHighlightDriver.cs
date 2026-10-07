using System.Collections.Generic;
using System.Linq;
using Card.Core;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 高亮驱动（M5-T6；FR-8.6）：把外部给定的 id 集合（可出牌/可攻击/嘲讽）写到视图高亮。
    /// 集合的计算（合法行动枚举）不属表现层——由 M5-T8 只读视图模型/权威侧喂入；
    /// 本驱动只做 O(1) 成员判断（调用方传 <see cref="HashSet{T}"/>）。
    /// </summary>
    public sealed class CardHighlightDriver
    {
        public void Apply(
            IReadOnlyList<CardView> views,
            IReadOnlyCollection<int> playableIds,
            IReadOnlyCollection<int> attackableIds,
            IReadOnlyCollection<int> tauntIds)
        {
            Guard.NotNull(views, nameof(views));
            Guard.NotNull(playableIds, nameof(playableIds));
            Guard.NotNull(attackableIds, nameof(attackableIds));
            Guard.NotNull(tauntIds, nameof(tauntIds));

            for (int i = 0; i < views.Count; i++)
            {
                CardHighlight highlight = CardHighlight.None;
                int? id = views[i].InstanceId;
                if (id.HasValue)
                {
                    if (playableIds.Contains(id.Value))
                    {
                        highlight |= CardHighlight.Playable;
                    }

                    if (attackableIds.Contains(id.Value))
                    {
                        highlight |= CardHighlight.Attackable;
                    }

                    if (tauntIds.Contains(id.Value))
                    {
                        highlight |= CardHighlight.Taunt;
                    }
                }

                views[i].SetHighlight(highlight);
            }
        }
    }
}
