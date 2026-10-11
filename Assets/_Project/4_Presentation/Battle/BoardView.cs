using System;
using System.Collections.Generic;
using Card.Core;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 战场区视图（M5-T3）：与 <see cref="HandView"/> 同形状，承载己方场上随从；卡牌子件走池化。
    /// 只读渲染，不回写状态。
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] internal CardView _cardPrefab = null!;
        [SerializeField] internal float _spacing = 140f;
        [SerializeField] internal int _prewarmCount = 7;

        private readonly List<CardView> _children = new List<CardView>();
        private CardViewPool? _pool;

        public int ChildCount => _children.Count;

        internal IReadOnlyList<CardView> Children => _children;

        internal CardViewPool Pool => EnsurePool();

        /// <summary>点击某个在区随从时触发，携带其 InstanceId（M6-T1）。</summary>
        public event Action<int>? CardClicked;

        /// <summary>按 InstanceId 查在区随从视图（定位/拾取用）；未找到返回 false。</summary>
        public bool TryGetCardView(int instanceId, out CardView? view)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i].InstanceId == instanceId)
                {
                    view = _children[i];
                    return true;
                }
            }

            view = null;
            return false;
        }

        public void SetCards(IReadOnlyList<ICardViewData> cards)
        {
            Guard.NotNull(cards, nameof(cards));

            CardViewPool pool = EnsurePool();
            CardZoneSync.Refresh(
                _children,
                cards,
                () => RentCard(pool),
                view => RecycleCard(pool, view),
                out HashSet<CardView>? entering);

            for (int i = 0; i < cards.Count; i++)
            {
                _children[i].SetData(cards[i]);
            }

            CardListLayout.Layout(_children, _spacing, entering);
        }

        private CardView RentCard(CardViewPool pool)
        {
            CardView added = pool.Rent(transform);
            added.BindClick(() => RaiseCardClicked(added));
            return added;
        }

        private void RecycleCard(CardViewPool pool, CardView view)
        {
            view.UnbindClick();
            pool.Return(view);
        }

        private void RaiseCardClicked(CardView view)
        {
            if (view.InstanceId.HasValue)
            {
                CardClicked?.Invoke(view.InstanceId.Value);
            }
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
