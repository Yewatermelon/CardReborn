using System.Collections.Generic;
using Card.Domain.Config;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>
    /// 区子视图身份对齐（M14-T2；CardZoneSync 经 HandView 集成验证）：
    /// 预制件带 <see cref="CardMotionView"/> 以观察滑位（MoveTo）与进场（BeginEntry）。
    /// </summary>
    [TestFixture]
    public sealed class CardZoneSyncTests
    {
        private GameObject _root = null!;
        private CardView _prefab = null!;
        private HandView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CardZoneSync_Test");
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _prefab.gameObject.AddComponent<CardMotionView>();
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

        private static CardViewData Minion(string name, int instanceId, int attack, int health)
        {
            return new CardViewData(name, "desc", 2, attack, health, "art", CardType.Minion, instanceId);
        }

        private static CardViewData CardBack()
        {
            return new CardViewData(string.Empty, string.Empty, 0, 0, 0, string.Empty, CardType.Spell);
        }

        /// <summary>把全部子视图的补间推到完成态（EditMode 无 Update，测试直接驱动）。</summary>
        private void SettleMotions()
        {
            foreach (CardView child in _view.Children)
            {
                if (child.TryGetComponent(out CardMotionView motion))
                {
                    motion.Tick(1f);
                }
            }
        }

        [Test]
        public void SetCards_ThreeCards_IdentityAlignedAndSettled()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("B", 2, 2, 2), Minion("C", 3, 3, 3) });
            SettleMotions();

            Assert.That(_view.Children[0].InstanceId, Is.EqualTo(1));
            Assert.That(_view.Children[1].InstanceId, Is.EqualTo(2));
            Assert.That(_view.Children[2].InstanceId, Is.EqualTo(3));
            foreach (CardView child in _view.Children)
            {
                Assert.That(child.GetComponent<CardMotionView>().IsPlaying, Is.False);
            }
        }

        [Test]
        public void SetCards_MiddleRemoval_SurvivorViewSlidesInsteadOfTeleport()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("B", 2, 2, 2), Minion("C", 3, 3, 3) });
            SettleMotions();
            CardView oldC = _view.Children[2];

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("C", 3, 3, 3) });

            Assert.That(_view.Children[1], Is.SameAs(oldC), "幸存卡视图必须按 InstanceId 对齐保留");
            CardMotionView motion = _view.Children[1].GetComponent<CardMotionView>();
            Assert.That(motion.IsPlaying, Is.True, "幸存卡应滑位而非瞬移");
            Assert.That(_view.Children[1].transform.localPosition.x, Is.EqualTo(100f).Within(0.001f), "滑位起点为旧槽位");
            motion.Tick(1f);
            Assert.That(_view.Children[1].transform.localPosition.x, Is.EqualTo(50f).Within(0.001f), "滑位终点为新槽位");
        }

        [Test]
        public void SetCards_MiddleSummon_NewViewEnters_OthersSlide()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("C", 3, 3, 3) });
            SettleMotions();
            CardView oldC = _view.Children[1];

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("X", 9, 9, 9), Minion("C", 3, 3, 3) });

            Assert.That(_view.ChildCount, Is.EqualTo(3));
            Assert.That(_view.Children[2], Is.SameAs(oldC), "C 的视图应滑到新槽位而非被复用改显示");
            CardMotionView entering = _view.Children[1].GetComponent<CardMotionView>();
            Assert.That(entering.IsPlaying, Is.True);
            Assert.That(_view.Children[1].GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "新卡入场从全透明淡入");
        }

        [Test]
        public void SetCards_DrawAppends_NewBackEnters_ExistingSlideWithoutEntry()
        {
            _view.SetCards(new List<ICardViewData> { CardBack(), CardBack() });
            SettleMotions();

            _view.SetCards(new List<ICardViewData> { CardBack(), CardBack(), CardBack() });

            Assert.That(_view.ChildCount, Is.EqualTo(3));
            // 存量牌背：居中布局重排使槽位平移（-50→-100 / +50→0），平滑滑位（MoveTo）；
            // 但绝不重新进场——alpha/scale 保持常态（这是与 entering 的判别标志）。
            for (int i = 0; i < 2; i++)
            {
                CardMotionView motion = _view.Children[i].GetComponent<CardMotionView>();
                Assert.That(motion.IsPlaying, Is.True, $"存量卡 {i} 因布局重排平滑滑位");
                Assert.That(_view.Children[i].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "存量卡不得重新淡入");
                Assert.That(_view.Children[i].transform.localScale, Is.EqualTo(Vector3.one), "存量卡不得重新缩放");
            }

            // 新抽牌背：进场动画（位置即落槽，从全透明淡入）。
            Assert.That(_view.Children[2].GetComponent<CardMotionView>().IsPlaying, Is.True, "新抽的牌背应播进场动画");
            Assert.That(_view.Children[2].GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "进场从全透明淡入");
        }

        [Test]
        public void SetCards_ShrinkFromEnd_SurvivorsSlide_NoFalseEntry()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("B", 2, 2, 2), Minion("C", 3, 3, 3) });
            SettleMotions();

            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("B", 2, 2, 2) });

            Assert.That(_view.ChildCount, Is.EqualTo(2));
            // 尾部收缩：幸存卡不得产生虚假进场（NoFalseEntry）——居中重排使槽位平移（-100→-50 / 0→+50），
            // 走 MoveTo 滑位而非淡入缩放。
            foreach (CardView child in _view.Children)
            {
                Assert.That(child.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "幸存卡不得重新淡入");
                Assert.That(child.transform.localScale, Is.EqualTo(Vector3.one), "幸存卡不得重新缩放入场");
            }

            // 滑位而非瞬移：Tick 后到达重排后的新槽位。
            foreach (CardView child in _view.Children)
            {
                child.GetComponent<CardMotionView>().Tick(1f);
            }

            Assert.That(_view.Children[0].transform.localPosition.x, Is.EqualTo(-50f).Within(0.001f));
            Assert.That(_view.Children[1].transform.localPosition.x, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void SetCards_EmptyThenRefill_NoNewInstantiate()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("B", 2, 2, 2), Minion("C", 3, 3, 3) });

            _view.SetCards(new List<ICardViewData>());
            _view.SetCards(new List<ICardViewData> { Minion("X", 7, 7, 7), Minion("Y", 8, 8, 8), Minion("Z", 9, 9, 9) });

            Assert.That(_view.Pool.CreatedCount, Is.EqualTo(3), "峰值后增删不得新建实例");
            Assert.That(_view.ChildCount, Is.EqualTo(3));
        }

        [Test]
        public void SetCards_Replacement_ChildrenMatchDataOrder()
        {
            _view.SetCards(new List<ICardViewData> { Minion("A", 1, 1, 1), Minion("B", 2, 2, 2), Minion("C", 3, 3, 3) });

            _view.SetCards(new List<ICardViewData> { Minion("C", 3, 3, 3), Minion("B", 2, 2, 2) });

            Assert.That(_view.Children[0].InstanceId, Is.EqualTo(3));
            Assert.That(_view.Children[1].InstanceId, Is.EqualTo(2));
            Assert.That(_view.Children[0]._nameText.text, Is.EqualTo("C"));
        }
    }
}
