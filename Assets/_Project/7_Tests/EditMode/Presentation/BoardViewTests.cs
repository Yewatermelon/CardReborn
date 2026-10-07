using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Card.Domain.Config;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class BoardViewTests
    {
        private GameObject _root = null!;
        private CardView _prefab = null!;
        private BoardView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("BoardView_Test");
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _prefab.gameObject.SetActive(false);
            _view = _root.AddComponent<BoardView>();
            _view._cardPrefab = _prefab;
            _view._spacing = 140f;
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
        public void SetCards_AddThenShrink_KeepsCountAligned()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });
            Assert.That(_view.ChildCount, Is.EqualTo(2));

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1) });
            Assert.That(_view.ChildCount, Is.EqualTo(1));
        }

        [Test]
        public void SetCards_RendersMinionAttackAndHealth()
        {
            _view.SetCards(new List<ICardViewData> { Minion("M", 4, 2) });

            CardView child = _view.transform.GetChild(0).GetComponent<CardView>();
            Assert.That(child._attackText.text, Is.EqualTo("4"));
            Assert.That(child._healthText.text, Is.EqualTo("2"));
            Assert.That(child._attackPanel.activeSelf, Is.True);
        }

        [Test]
        public void SetCards_TwoCards_LayoutIsCentered()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });

            Assert.That(_view.transform.GetChild(0).localPosition.x, Is.EqualTo(-70f).Within(0.001f));
            Assert.That(_view.transform.GetChild(1).localPosition.x, Is.EqualTo(70f).Within(0.001f));
        }
    }
}
