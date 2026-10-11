using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>
    /// 待确认可视组件测试（M14-T1）：进入/重复进入/回退/确认/无条件复位，
    /// 以及 CardView.SetData 复用复位契约（AC-8，M6-T5"复用即全量复位"）。
    /// </summary>
    [TestFixture]
    public class CardPendingViewTests
    {
        private GameObject _root = null!;
        private RectTransform _rect = null!;
        private CardPendingView _pending = null!;
        private CanvasGroup _group = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CardPending_Test", typeof(RectTransform));
            _rect = _root.GetComponent<RectTransform>();
            _rect.anchoredPosition = new Vector2(10f, 20f);
            _group = _root.AddComponent<CanvasGroup>();
            _pending = _root.AddComponent<CardPendingView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ShowPending_SetsAlphaOffset_AndIsPending()
        {
            _pending.ShowPending();

            Assert.That(_pending.IsPending, Is.True);
            Assert.That(_group.alpha, Is.EqualTo(CardPendingView.PendingAlpha));
            Assert.That(_rect.anchoredPosition, Is.EqualTo(new Vector2(10f, 20f + CardPendingView.PendingOffsetY)));
        }

        [Test]
        public void ShowPending_Twice_DoesNotDoubleOffset()
        {
            _pending.ShowPending();
            _pending.ShowPending();

            Assert.That(_rect.anchoredPosition.y,
                Is.EqualTo(20f + CardPendingView.PendingOffsetY));
        }

        [Test]
        public void Rollback_RestoresAlphaAndPosition()
        {
            _pending.ShowPending();

            _pending.Rollback();

            Assert.That(_pending.IsPending, Is.False);
            Assert.That(_group.alpha, Is.EqualTo(1f));
            Assert.That(_rect.anchoredPosition, Is.EqualTo(new Vector2(10f, 20f)));
        }

        [Test]
        public void Confirm_RestoresAlphaAndPosition()
        {
            _pending.ShowPending();

            _pending.Confirm();

            Assert.That(_pending.IsPending, Is.False);
            Assert.That(_group.alpha, Is.EqualTo(1f));
            Assert.That(_rect.anchoredPosition, Is.EqualTo(new Vector2(10f, 20f)));
        }

        [Test]
        public void Stop_WithoutPending_IsSafeNoOp()
        {
            Assert.That(() => _pending.Stop(), Throws.Nothing);
            Assert.That(_group.alpha, Is.EqualTo(1f));
        }

        [Test]
        public void SetData_ResetsPendingVisual_OnReuse()
        {
            // AC-8（复用即全量复位契约）：pending 中的卡被 SetData 复用 → 视觉复位
            //（无 RectTransform 时位置不参与，alpha/IsPending 复位与位置复位同走 Stop 路径）
            CardView card = PresentationTestPrefabs.CreateCardViewPrefab();
            CardPendingView pending = card.gameObject.AddComponent<CardPendingView>();
            try
            {
                pending.ShowPending();
                Assert.That(pending.IsPending, Is.True);

                card.SetData(new CardViewData("重用卡", "d", 1, 2, 3, "art", CardType.Minion, 5));

                Assert.That(pending.IsPending, Is.False);
                Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(card.gameObject);
            }
        }
    }
}
