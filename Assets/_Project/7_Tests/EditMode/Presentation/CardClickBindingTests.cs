using System.Collections.Generic;
using Card.Domain.Config;
using Card.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>M6-T1：CardView 点击绑定与 HandView/BoardView 点击事件（池化卡不串台、解绑后不触发）。</summary>
    [TestFixture]
    public sealed class CardClickBindingTests
    {
        private GameObject _root = null!;
        private CardView _prefab = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _prefab.gameObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        private TView MakeListView<TView>() where TView : Component
        {
            var view = _root.AddComponent<TView>();
            switch (view)
            {
                case HandView hand:
                    hand._cardPrefab = _prefab;
                    hand._spacing = 10f;
                    hand._prewarmCount = 0;
                    break;
                case BoardView board:
                    board._cardPrefab = _prefab;
                    board._spacing = 10f;
                    board._prewarmCount = 0;
                    break;
            }

            return view;
        }

        private static CardViewData Minion(int instanceId)
        {
            return new CardViewData("n", "d", 1, 2, 3, "a", CardType.Minion, instanceId: instanceId);
        }

        [Test]
        public void CardView_BindClick_ButtonInvokeFiresCallback()
        {
            CardView card = _prefab;
            int fires = 0;
            card.BindClick(() => fires++);

            card.GetComponent<Button>().onClick.Invoke();

            Assert.That(fires, Is.EqualTo(1));
        }

        [Test]
        public void CardView_UnbindClick_DoesNotFire()
        {
            CardView card = _prefab;
            int fires = 0;
            card.BindClick(() => fires++);
            card.UnbindClick();

            card.GetComponent<Button>().onClick.Invoke();

            Assert.That(fires, Is.EqualTo(0));
        }

        [Test]
        public void CardView_RebindClick_OnlyLatestFiresOnce()
        {
            CardView card = _prefab;
            int first = 0;
            int second = 0;
            card.BindClick(() => first++);
            card.BindClick(() => second++);

            card.GetComponent<Button>().onClick.Invoke();

            Assert.That(first, Is.EqualTo(0));
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void HandView_CardClicked_CarriesInstanceId()
        {
            HandView hand = MakeListView<HandView>();
            var ids = new List<int>();
            hand.CardClicked += id => ids.Add(id);
            hand.SetCards(new ICardViewData[] { Minion(42) });

            hand.Children[0].GetComponent<Button>().onClick.Invoke();

            Assert.That(ids, Is.EqualTo(new[] { 42 }));
        }

        [Test]
        public void HandView_RemovedCard_DoesNotFire()
        {
            HandView hand = MakeListView<HandView>();
            int fires = 0;
            hand.CardClicked += id => fires++;
            hand.SetCards(new ICardViewData[] { Minion(42) });
            CardView pooled = hand.Children[0];
            hand.SetCards(new ICardViewData[0]);

            pooled.GetComponent<Button>().onClick.Invoke();

            Assert.That(fires, Is.EqualTo(0));
            Assert.That(hand.ChildCount, Is.EqualTo(0));
        }

        [Test]
        public void BoardView_CardClicked_AndTryGetCardView()
        {
            BoardView board = MakeListView<BoardView>();
            var ids = new List<int>();
            board.CardClicked += id => ids.Add(id);
            board.SetCards(new ICardViewData[] { Minion(7), Minion(8) });

            board.Children[1].GetComponent<Button>().onClick.Invoke();

            Assert.That(ids, Is.EqualTo(new[] { 8 }));
            Assert.That(board.TryGetCardView(7, out CardView? found), Is.True);
            Assert.That(found!.InstanceId, Is.EqualTo(7));
            Assert.That(board.TryGetCardView(999, out CardView? missing), Is.False);
            Assert.That(missing, Is.Null);
        }
    }
}
