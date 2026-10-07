using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Presentation.Battle.Log
{
    /// <summary>
    /// 战斗日志视图（M5-T7；FR-5.12）。追加事件 → 格式化 → 落条目 → 滚到底。
    /// 条目对象走池（03 §5.8）：超过 <see cref="_maxEntries"/> 时回收最旧条目形成滚动窗口。
    /// 只读：只消费事件、只写自身 UI，不判规则不改状态。装配（MatchEventPump → Append）属 M6。
    /// </summary>
    public sealed class BattleLogView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _entryPrefab = null!;
        [SerializeField] private Transform _content = null!;
        [SerializeField] private ScrollRect? _scrollRect;
        [SerializeField] private int _maxEntries = 100;

        private ObjectPool<TMP_Text>? _pool;
        private readonly List<TMP_Text> _entries = new List<TMP_Text>();

        public int EntryCount => _entries.Count;

        /// <summary>当前在屏条目（追加序，供测试断言）。</summary>
        internal IReadOnlyList<TMP_Text> Entries => _entries;

        /// <summary>池累计创建数（供测试断言复用）。</summary>
        internal int CreatedCount => _pool?.CreatedCount ?? 0;

        /// <summary>测试注入入口（EditMode 下 Awake/序列化赋值不可靠，显式注入）。</summary>
        internal void InitializeForTests(TMP_Text entryPrefab, Transform content, ScrollRect? scrollRect, int maxEntries)
        {
            _entryPrefab = entryPrefab;
            _content = content;
            _scrollRect = scrollRect;
            _maxEntries = maxEntries;
        }

        /// <summary>格式化并追加一条事件日志，随后滚到底。</summary>
        public void Append(GameEvent e)
        {
            Guard.NotNull(e, nameof(e));
            AppendLine(BattleLogFormatter.Format(e));
        }

        /// <summary>全部条目归还池。</summary>
        public void Clear()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                RecycleAt(i);
            }
        }

        private void AppendLine(string line)
        {
            while (_entries.Count >= _maxEntries)
            {
                RecycleAt(0);
            }

            TMP_Text entry = EnsurePool().Rent();
            entry.text = line;
            entry.transform.SetParent(_content, false);
            entry.transform.SetAsLastSibling();
            entry.gameObject.SetActive(true);
            _entries.Add(entry);

            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void RecycleAt(int index)
        {
            TMP_Text entry = _entries[index];
            _entries.RemoveAt(index);
            entry.gameObject.SetActive(false);
            EnsurePool().Return(entry);
        }

        private ObjectPool<TMP_Text> EnsurePool()
        {
            return _pool ??= new ObjectPool<TMP_Text>(CreateEntry);
        }

        private TMP_Text CreateEntry()
        {
            TMP_Text entry = Instantiate(_entryPrefab, _content);
            entry.gameObject.SetActive(false);
            return entry;
        }
    }
}
