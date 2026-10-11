using System.Collections.Generic;
using System.Linq;
using Card.Presentation.Battle.Feedback;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 卡牌列表的水平居中布局（M5-T2）：x = (i - (n-1)/2) * spacing。
    /// M14-T2：子视图带 <see cref="CardMotionView"/> 时改走插值——入场卡 BeginEntry、
    /// 存量卡 MoveTo（Docs/05 §5.5 位置插值）；无运动组件保持旧的原地赋值行为。
    /// </summary>
    internal static class CardListLayout
    {
        public static void Layout(
            IReadOnlyList<CardView> children,
            float spacing,
            IReadOnlyCollection<CardView>? entering = null)
        {
            int n = children.Count;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * spacing;
                var target = new Vector2(x, 0f);

                if (!children[i].TryGetComponent(out CardMotionView motion))
                {
                    children[i].transform.localPosition = new Vector3(x, 0f, 0f);
                    continue;
                }

                if (entering != null && entering.Contains(children[i]))
                {
                    motion.BeginEntry(target);
                }
                else
                {
                    motion.MoveTo(target);
                }
            }
        }
    }
}
