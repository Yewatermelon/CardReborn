using System.Collections.Generic;
using Card.Core;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 手牌区视图（M5-T3）：卡牌子件走 <see cref="CardViewPool"/> 租还，峰值后零 Instantiate。
    /// 只读渲染，不回写状态。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] internal CardView _cardPrefab = null!;
        [SerializeField] internal float _spacing = 120f;
        [SerializeField] internal int _prewarmCount = 4;

        private readonly List<CardView> _children = new List<CardView>();
        private CardViewPool? _pool;

        public int ChildCount => _children.Count;

        internal IReadOnlyList<CardView> Children => _children;

        internal CardViewPool Pool => EnsurePool();

        public void SetCards(IReadOnlyList<ICardViewData> cards)
        {
            Guard.NotNull(cards, nameof(cards));

            CardViewPool pool = EnsurePool();
            while (_children.Count > cards.Count)
            {
                int last = _children.Count - 1;
                pool.Return(_children[last]);
                _children.RemoveAt(last);
            }

            while (_children.Count < cards.Count)
            {
                _children.Add(pool.Rent(transform));
            }

            for (int i = 0; i < cards.Count; i++)
            {
                _children[i].SetData(cards[i]);
            }

            CardListLayout.Layout(_children, _spacing);
        }

        private CardViewPool EnsurePool()
        {
            if (_pool != null)
            {
                return _pool;
            }

            var poolGo = new GameObject("CardViewPool");
            poolGo.SetActive(false);
            poolGo.transform.SetParent(transform, false);
            _pool = new CardViewPool(_cardPrefab, poolGo.transform, _prewarmCount);
            return _pool;
        }
    }
}
