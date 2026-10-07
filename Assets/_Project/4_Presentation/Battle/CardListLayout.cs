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

    /// <summary>卡牌列表子件数量对齐（M5-T2）：少建多删，编辑模式下用 DestroyImmediate。</summary>
    internal static class CardListSync
    {
        public static void Sync(Transform parent, CardView prefab, List<CardView> children, int count)
        {
            while (children.Count > count)
            {
                int last = children.Count - 1;
                GameObject go = children[last].gameObject;
                children.RemoveAt(last);
                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(go);
                }
                else
                {
                    Object.DestroyImmediate(go);
                }
            }

            while (children.Count < count)
            {
                children.Add(Object.Instantiate(prefab, parent));
            }
        }
    }
}
