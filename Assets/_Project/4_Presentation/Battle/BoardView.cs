using System.Collections.Generic;
using Card.Core;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 战场区视图（M5-T2）：与 <see cref="HandView"/> 同形状，承载己方场上随从。
    /// 只读渲染，不回写状态。
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] internal CardView _cardPrefab = null!;
        [SerializeField] internal float _spacing = 140f;

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
