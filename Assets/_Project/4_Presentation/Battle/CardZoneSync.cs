using System;
using System.Collections.Generic;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 区子视图与数据的身份对齐（M14-T2，Docs/05 §5.5 位置插值的前提）：
    /// 按 <see cref="ICardViewData.InstanceId"/> 把既有视图匹配到同卡（滑位而非整列重绘），
    /// 未命中的数据租新视图（进场），淘汰视图还池。牌背（InstanceId=null）按占位顺序对齐。
    /// 对齐后 children[0..cards.Count) 与 cards 同序，且 children[i].InstanceId == cards[i].InstanceId。
    /// </summary>
    internal static class CardZoneSync
    {
        /// <summary>
        /// 就地对齐。<paramref name="rent"/> 租新视图（追加并计入 entering）；
        /// <paramref name="recycle"/> 还池（含解绑等清理）；<paramref name="entering"/> 为本次新租集合（无新租为 null）。
        /// </summary>
        public static void Refresh(
            List<CardView> children,
            IReadOnlyList<ICardViewData> cards,
            Func<CardView> rent,
            Action<CardView> recycle,
            out HashSet<CardView>? entering)
        {
            HashSet<CardView>? rented = null;

            for (int i = 0; i < cards.Count; i++)
            {
                int found = FindMatch(children, i, cards[i].InstanceId, rented);
                if (found < 0)
                {
                    CardView added = rent();
                    children.Add(added);
                    rented ??= new HashSet<CardView>();
                    rented.Add(added);
                    found = children.Count - 1;
                }

                if (found != i)
                {
                    (children[i], children[found]) = (children[found], children[i]);
                }
            }

            for (int j = children.Count - 1; j >= cards.Count; j--)
            {
                recycle(children[j]);
                children.RemoveAt(j);
            }

            entering = rented;
        }

        /// <summary>从 <paramref name="from"/> 起找 InstanceId 相等的视图；本次新租视图携带池内脏 Id，不参与匹配。</summary>
        private static int FindMatch(
            List<CardView> children,
            int from,
            int? instanceId,
            HashSet<CardView>? rented)
        {
            for (int j = from; j < children.Count; j++)
            {
                if (rented != null && rented.Contains(children[j]))
                {
                    continue;
                }

                if (Nullable.Equals(children[j].InstanceId, instanceId))
                {
                    return j;
                }
            }

            return -1;
        }
    }
}
