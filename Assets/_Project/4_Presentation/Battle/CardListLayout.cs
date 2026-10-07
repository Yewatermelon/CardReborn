using System.Collections.Generic;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>卡牌列表的水平居中布局（M5-T2）：x = (i - (n-1)/2) * spacing。</summary>
    internal static class CardListLayout
    {
        public static void Layout(IReadOnlyList<CardView> children, float spacing)
        {
            int n = children.Count;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * spacing;
                children[i].transform.localPosition = new Vector3(x, 0f, 0f);
            }
        }
    }
}
