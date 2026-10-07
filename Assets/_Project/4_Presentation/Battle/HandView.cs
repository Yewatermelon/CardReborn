using System;
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

        /// <summary>点击某张在区卡牌时触发，携带其 InstanceId（无 InstanceId 的配置态卡不触发，M6-T1）。</summary>
        public event Action<int>? CardClicked;

        /// <summary>按 InstanceId 查在区卡牌视图（定位/拾取用）；未找到返回 false。</summary>
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
            while (_children.Count > cards.Count)
            {
                int last = _children.Count - 1;
                _children[last].UnbindClick();
                pool.Return(_children[last]);
                _children.RemoveAt(last);
            }

            while (_children.Count < cards.Count)
            {
                CardView added = pool.Rent(transform);
                added.BindClick(() => RaiseCardClicked(added));
                _children.Add(added);
            }

            for (int i = 0; i < cards.Count; i++)
            {
                _children[i].SetData(cards[i]);
            }

            CardListLayout.Layout(_children, _spacing);
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
