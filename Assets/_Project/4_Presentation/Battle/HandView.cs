using System.Collections.Generic;
using Card.Core;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 手牌区视图（M5-T2）：按数据增删 <see cref="CardView"/> 子件并水平居中排布。
    /// 只读渲染，不回写状态；增删暂用 Instantiate/Destroy，对象池在 M5-T3 接入。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] internal CardView _cardPrefab = null!;
        [SerializeField] internal float _spacing = 120f;

        private readonly List<CardView> _children = new List<CardView>();

        public int ChildCount => _children.Count;

        public void SetCards(IReadOnlyList<ICardViewData> cards)
        {
            Guard.NotNull(cards, nameof(cards));

            CardListSync.Sync(transform, _cardPrefab, _children, cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                _children[i].SetData(cards[i]);
            }

            CardListLayout.Layout(_children, _spacing);
        }
    }
}
