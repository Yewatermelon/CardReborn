using System.Collections.Generic;
using Card.Domain.Config;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>高亮驱动（M5-T6；FR-8.6）：外部给定 id 集合 → 视图高亮旗标；不判规则。</summary>
    [TestFixture]
    internal sealed class CardHighlightDriverTests
    {
        private readonly List<CardView> _views = new List<CardView>();
        private CardHighlightDriver _driver = null!;
        private CardView _prefab = null!;

        [SetUp]
        public void SetUp()
        {
            _driver = new CardHighlightDriver();
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            int?[] ids = { 10, 20, null };
            foreach (int? id in ids)
            {
                CardView view = Object.Instantiate(_prefab);
                view.SetData(new CardViewData("n", "d", 2, 1, 1, "art", CardType.Minion, id));
                _views.Add(view);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CardView view in _views)
            {
                Object.DestroyImmediate(view.gameObject);
            }

            _views.Clear();
            Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void Apply_PlayableIds_MarkOnlyMatchingViews()
        {
            _driver.Apply(_views, new HashSet<int> { 10 }, Empty(), Empty());

            Assert.IsTrue(_views[0]._playableHighlight.activeSelf);
            Assert.IsFalse(_views[1]._playableHighlight.activeSelf);
            Assert.IsFalse(_views[2]._playableHighlight.activeSelf);
        }

        [Test]
        public void Apply_AttackableAndTaunt_CombineOnSameView()
        {
            _driver.Apply(_views, Empty(), new HashSet<int> { 20 }, new HashSet<int> { 20 });

            Assert.IsTrue(_views[1]._attackableHighlight.activeSelf);
            Assert.IsTrue(_views[1]._tauntHighlight.activeSelf);
            Assert.IsFalse(_views[1]._playableHighlight.activeSelf);
        }

        [Test]
        public void Apply_NullInstanceId_AlwaysCleared()
        {
            var all = new HashSet<int> { 10, 20 };

            _driver.Apply(_views, all, all, all);

            Assert.IsFalse(_views[2]._playableHighlight.activeSelf);
            Assert.IsFalse(_views[2]._attackableHighlight.activeSelf);
            Assert.IsFalse(_views[2]._tauntHighlight.activeSelf);
        }

        [Test]
        public void Apply_EmptySets_ClearPreviousHighlights()
        {
            var all = new HashSet<int> { 10, 20 };
            _driver.Apply(_views, all, all, all);

            _driver.Apply(_views, Empty(), Empty(), Empty());

            foreach (CardView view in _views)
            {
                Assert.IsFalse(view._playableHighlight.activeSelf);
                Assert.IsFalse(view._attackableHighlight.activeSelf);
                Assert.IsFalse(view._tauntHighlight.activeSelf);
            }
        }

        private static HashSet<int> Empty()
        {
            return new HashSet<int>();
        }
    }
}
