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
        public void SetCards_AddThenShrink_KeepsCountAlignedAndParksReturned()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });
            Assert.That(_view.ChildCount, Is.EqualTo(2));

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1) });

            Assert.That(_view.ChildCount, Is.EqualTo(1));
            Assert.That(_view.Pool.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public void SetCards_RendersMinionAttackAndHealth()
        {
            _view.SetCards(new List<ICardViewData> { Minion("M", 4, 2) });

            CardView child = _view.Children[0];
            Assert.That(child._attackText.text, Is.EqualTo("4"));
            Assert.That(child._healthText.text, Is.EqualTo("2"));
            Assert.That(child._attackPanel.activeSelf, Is.True);
        }

        [Test]
        public void SetCards_TwoCards_LayoutIsCentered()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2) });

            Assert.That(_view.Children[0].transform.localPosition.x, Is.EqualTo(-70f).Within(0.001f));
            Assert.That(_view.Children[1].transform.localPosition.x, Is.EqualTo(70f).Within(0.001f));
        }

        [Test]
        public void SetCards_AfterPeakShrinkAndRegrow_NoNewInstantiate()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1) });
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1), Minion("B", 2, 2), Minion("C", 3, 3) });

            Assert.That(_view.Pool.CreatedCount, Is.EqualTo(3), "峰值后增删不得新建实例");
            Assert.That(_view.ChildCount, Is.EqualTo(3));
        }
    }
}
