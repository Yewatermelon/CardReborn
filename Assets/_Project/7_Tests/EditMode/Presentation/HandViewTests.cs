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
            Assert.That(_view.transform.GetChild(0).GetComponent<CardView>()._nameText.text, Is.EqualTo("A"));
            Assert.That(_view.transform.GetChild(2).GetComponent<CardView>()._nameText.text, Is.EqualTo("C"));
        }

        [Test]
        public void SetCards_ShrinkFromThreeToOne_DestroysTwo()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });

            _view.SetCards(new List<ICardViewData> { Minion("A2", 5, 5) });

            Assert.That(_view.ChildCount, Is.EqualTo(1));
            Assert.That(_view.transform.childCount, Is.EqualTo(1));
            Assert.That(_view.transform.GetChild(0).GetComponent<CardView>()._nameText.text, Is.EqualTo("A2"));
        }

        [Test]
        public void SetCards_SameCount_ReusesChildren()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });
            CardView first = _view.transform.GetChild(0).GetComponent<CardView>();
            CardView second = _view.transform.GetChild(1).GetComponent<CardView>();

            _view.SetCards(new List<ICardViewData> { Minion("X", 9, 9), Minion("Y", 8, 8) });

            Assert.That(_view.ChildCount, Is.EqualTo(2));
            Assert.That(_view.transform.GetChild(0).GetComponent<CardView>(), Is.SameAs(first));
            Assert.That(_view.transform.GetChild(1).GetComponent<CardView>(), Is.SameAs(second));
            Assert.That(first._nameText.text, Is.EqualTo("X"));
            Assert.That(second._nameText.text, Is.EqualTo("Y"));
        }

        [Test]
        public void SetCards_ThreeCards_LayoutIsCentered()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });

            Assert.That(_view.transform.GetChild(0).localPosition.x, Is.EqualTo(-100f).Within(0.001f));
            Assert.That(_view.transform.GetChild(1).localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_view.transform.GetChild(2).localPosition.x, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void SetCards_Empty_ClearsAll()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });

            _view.SetCards(new List<ICardViewData>());

            Assert.That(_view.ChildCount, Is.EqualTo(0));
            Assert.That(_view.transform.childCount, Is.EqualTo(0));
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
