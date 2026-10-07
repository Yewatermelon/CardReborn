using System.Linq;
using Card.Domain.Match;
using Card.Presentation.Battle.Log;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>M5-T7：BattleLogView 追加/滚动/容量回收/池复用。</summary>
    [TestFixture]
    public sealed class BattleLogViewTests
    {
        private GameObject _root = null!;
        private BattleLogView _view = null!;
        private Transform _content = null!;
        private ScrollRect _scroll = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            var entryGo = new GameObject("Entry", typeof(RectTransform));
            entryGo.transform.SetParent(_root.transform, false);
            var entryPrefab = entryGo.AddComponent<TextMeshProUGUI>();
            entryGo.SetActive(false);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_root.transform, false);
            _content = contentGo.transform;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(_root.transform, false);
            _scroll = scrollGo.AddComponent<ScrollRect>();
            _scroll.content = (RectTransform)_content;

            _view = _root.AddComponent<BattleLogView>();
            _view.InitializeForTests(entryPrefab, _content, _scroll, maxEntries: 3);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Append_AddsEntryWithFormattedText()
        {
            _view.Append(new CardPlayedEvent(0, 21));

            Assert.That(_view.EntryCount, Is.EqualTo(1));
            var entry = _view.Entries.Single();
            StringAssert.Contains("21", entry.text);
            Assert.That(entry.gameObject.activeSelf, Is.True);
            Assert.That(entry.transform.parent, Is.EqualTo(_content));
        }

        [Test]
        public void Append_KeepsEventOrder()
        {
            _view.Append(new CardPlayedEvent(0, 1));
            _view.Append(new CardPlayedEvent(0, 2));

            StringAssert.Contains("1", _view.Entries[0].text);
            StringAssert.Contains("2", _view.Entries[1].text);
        }

        [Test]
        public void Append_ScrollsToBottom()
        {
            _scroll.verticalNormalizedPosition = 0.5f;

            _view.Append(new CardPlayedEvent(0, 1));

            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(0f));
        }

        [Test]
        public void Append_OverMax_RecyclesOldest()
        {
            for (int i = 1; i <= 5; i++)
            {
                _view.Append(new CardPlayedEvent(0, i));
            }

            Assert.That(_view.EntryCount, Is.EqualTo(3));
            StringAssert.Contains("3", _view.Entries[0].text);
            StringAssert.Contains("4", _view.Entries[1].text);
            StringAssert.Contains("5", _view.Entries[2].text);
        }

        [Test]
        public void Append_Churn_DoesNotCreateNewObjects()
        {
            for (int i = 1; i <= 20; i++)
            {
                _view.Append(new CardPlayedEvent(0, i));
            }

            Assert.That(_view.EntryCount, Is.EqualTo(3));
            Assert.That(_view.CreatedCount, Is.EqualTo(3));
        }

        [Test]
        public void Clear_ReturnsAllEntries_AndReuseAfter()
        {
            for (int i = 1; i <= 3; i++)
            {
                _view.Append(new CardPlayedEvent(0, i));
            }

            _view.Clear();

            Assert.That(_view.EntryCount, Is.EqualTo(0));
            foreach (Transform child in _content)
            {
                Assert.That(child.gameObject.activeSelf, Is.False);
            }

            _view.Append(new CardPlayedEvent(0, 9));
            Assert.That(_view.CreatedCount, Is.EqualTo(3));
        }
    }
}
