using Card.Core;
using Card.Presentation.Battle.Feedback;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// <see cref="CardView"/> 的对象池封装（M5-T3）：借出激活挂到指定父级，归还禁用并停回池容器。
    /// 内部复用 M1-T6 <see cref="ObjectPool{T}"/>；峰值之后增删不再触发 Instantiate。
    /// </summary>
    internal sealed class CardViewPool
    {
        private readonly Transform _poolRoot;
        private readonly ObjectPool<CardView> _pool;

        public CardViewPool(CardView prefab, Transform poolRoot, int prewarmCount = 0)
        {
            Guard.NotNull(prefab, nameof(prefab));
            _poolRoot = Guard.NotNull(poolRoot, nameof(poolRoot));
            _pool = new ObjectPool<CardView>(() => Create(prefab, poolRoot), prewarmCount);
        }

        /// <summary>工厂累计 Instantiate 数（含预热）；对局中该值不再增长即"无新建"。</summary>
        public int CreatedCount => _pool.CreatedCount;

        public int IdleCount => _pool.IdleCount;

        public int ActiveCount => _pool.ActiveCount;

        public CardView Rent(Transform parent)
        {
            Guard.NotNull(parent, nameof(parent));
            CardView view = _pool.Rent();
            view.transform.SetParent(parent, false);
            view.gameObject.SetActive(true);
            return view;
        }

        public void Return(CardView view)
        {
            Guard.NotNull(view, nameof(view));
            ResetView(view);
            view.gameObject.SetActive(false);
            view.transform.SetParent(_poolRoot, false);
            _pool.Return(view);
        }

        private static CardView Create(CardView prefab, Transform poolRoot)
        {
            CardView view = Object.Instantiate(prefab, poolRoot);
            ResetView(view);
            view.gameObject.SetActive(false);
            return view;
        }

        /// <summary>归还/出厂复位（03 §5.8 第 2 条）：高亮清零、停止淡出、alpha 归 1。</summary>
        private static void ResetView(CardView view)
        {
            view.SetHighlight(CardHighlight.None);

            CardFadeOutView fade = view.GetComponent<CardFadeOutView>();
            if (fade != null)
            {
                fade.Stop();
            }

            CanvasGroup group = view.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
            }
        }
    }
}
