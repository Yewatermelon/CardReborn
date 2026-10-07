using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Card.Domain.Config;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class HandViewTests
    {
        private GameObject _root = null!;
        private CardView _prefab = null!;
        private HandView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HandView_Test");
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _prefab.gameObject.SetActive(false);
            _view = _root.AddComponent<HandView>();
            _view._cardPrefab = _prefab;
            _view._spacing = 100f;
            _view._prewarmCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        private static CardViewData Minion(string name, int attack, int health)
        {
            return new CardViewData(name, "desc", 2, attack, health, "art", CardType.Minion);
        }

        [Test]
        public void SetCards_ThreeCards_CreatesThreeChildren()
        {
            var cards = new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) };

            _view.SetCards(cards);

            Assert.That(_view.ChildCount, Is.EqualTo(3));
            Assert.That(_view.Children[0]._nameText.text, Is.EqualTo("A"));
            Assert.That(_view.Children[2]._nameText.text, Is.EqualTo("C"));
            Assert.That(_view.Pool.CreatedCount, Is.EqualTo(3));
        }

        [Test]
        public void SetCards_ShrinkFromThreeToOne_ReturnsTwoToPool()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });

            _view.SetCards(new List<ICardViewData> { Minion("A2", 5, 5) });

            Assert.That(_view.ChildCount, Is.EqualTo(1));
            Assert.That(_view.Children[0]._nameText.text, Is.EqualTo("A2"));
            Assert.That(_view.Pool.IdleCount, Is.EqualTo(2));
            Assert.That(_view.Pool.CreatedCount, Is.EqualTo(3));
        }

        [Test]
        public void SetCards_SameCount_ReusesChildren()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });
            CardView first = _view.Children[0];
            CardView second = _view.Children[1];

            _view.SetCards(new List<ICardViewData> { Minion("X", 9, 9), Minion("Y", 8, 8) });

            Assert.That(_view.ChildCount, Is.EqualTo(2));
            Assert.That(_view.Children[0], Is.SameAs(first));
            Assert.That(_view.Children[1], Is.SameAs(second));
            Assert.That(first._nameText.text, Is.EqualTo("X"));
            Assert.That(second._nameText.text, Is.EqualTo("Y"));
        }

        [Test]
        public void SetCards_ThreeCards_LayoutIsCentered()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });

            Assert.That(_view.Children[0].transform.localPosition.x, Is.EqualTo(-100f).Within(0.001f));
            Assert.That(_view.Children[1].transform.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_view.Children[2].transform.localPosition.x, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void SetCards_Empty_ReturnsAllToPool()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });

            _view.SetCards(new List<ICardViewData>());

            Assert.That(_view.ChildCount, Is.EqualTo(0));
            Assert.That(_view.Pool.IdleCount, Is.EqualTo(2));
            Assert.That(_view.Pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void SetCards_AfterPeakShrinkAndRegrow_NoNewInstantiateAndReusesInstances()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });
            var originals = new List<CardView> { _view.Children[0], _view.Children[1], _view.Children[2] };

            _view.SetCards(new List<ICardViewData>());
            _view.SetCards(new List<ICardViewData> { Minion("X", 9, 9), Minion("Y", 8, 8), Minion("Z", 7, 7) });

            Assert.That(_view.Pool.CreatedCount, Is.EqualTo(3), "峰值后增删不得新建实例");
            Assert.That(_view.ChildCount, Is.EqualTo(3));
            foreach (CardView child in _view.Children)
            {
                Assert.That(originals, Does.Contain(child));
                Assert.That(child.gameObject.activeSelf, Is.True);
            }
        }

        [Test]
        public void SetCards_WithPrewarm_FirstSyncCreatesOnlyPrewarmCount()
        {
            _view._prewarmCount = 5;

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1) });

            Assert.That(_view.Pool.CreatedCount, Is.EqualTo(5));
            Assert.That(_view.Pool.IdleCount, Is.EqualTo(4));
            Assert.That(_view.ChildCount, Is.EqualTo(1));
        }

        [Test]
        public void SetCards_Null_ThrowsArgumentNullException()
        {
            Assert.That(
                () => _view.SetCards(null!),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("cards"));
        }
    }
}
