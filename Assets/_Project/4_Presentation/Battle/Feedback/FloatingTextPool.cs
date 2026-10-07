using System.Collections.Generic;
using Card.Core;
using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 浮动文字池（M5-T6；03 §5.8：伤害数字必须走池）。复用 M1-T6 <see cref="ObjectPool{T}"/>；
    /// 自持在借列表以支持 <see cref="ReclaimFinished"/> 批量回收播完对象。
    /// </summary>
    public sealed class FloatingTextPool
    {
        private readonly Transform _poolRoot;
        private readonly ObjectPool<FloatingTextView> _pool;
        private readonly List<FloatingTextView> _rented = new List<FloatingTextView>();

        public FloatingTextPool(FloatingTextView prefab, Transform poolRoot, int prewarmCount = 0)
        {
            Guard.NotNull(prefab, nameof(prefab));
            _poolRoot = Guard.NotNull(poolRoot, nameof(poolRoot));
            _pool = new ObjectPool<FloatingTextView>(() => Create(prefab, poolRoot), prewarmCount);
        }

        public int CreatedCount => _pool.CreatedCount;

        public int ActiveCount => _rented.Count;

        public int IdleCount => _pool.IdleCount;

        public FloatingTextView Rent(Transform parent)
        {
            Guard.NotNull(parent, nameof(parent));
            FloatingTextView view = _pool.Rent();
            view.transform.SetParent(parent, false);
            view.gameObject.SetActive(true);
            _rented.Add(view);
            return view;
        }

        public void Return(FloatingTextView view)
        {
            Guard.NotNull(view, nameof(view));
            if (!_rented.Remove(view))
            {
                throw new System.InvalidOperationException("归还了非本池借出或已归还的浮动文字。");
            }

            view.Stop();
            view.transform.SetParent(_poolRoot, false);
            _pool.Return(view);
        }

        /// <summary>回收全部播完（!IsPlaying）的在借视图，返回本次回收数。</summary>
        public int ReclaimFinished()
        {
            int reclaimed = 0;
            for (int i = _rented.Count - 1; i >= 0; i--)
            {
                if (!_rented[i].IsPlaying)
                {
                    Return(_rented[i]);
                    reclaimed++;
                }
            }

            return reclaimed;
        }

        private static FloatingTextView Create(FloatingTextView prefab, Transform poolRoot)
        {
            FloatingTextView view = Object.Instantiate(prefab, poolRoot);
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
