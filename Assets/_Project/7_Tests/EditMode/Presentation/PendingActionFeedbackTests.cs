using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>
    /// 表现先行协调器测试（M14-T1，Docs/05 §6.1）：上行即时待确认视觉、
    /// 权威接受确认复位、拒绝回退 + 原因浮字、对手步不动视觉、重复包视同接受、
    /// 版本空洞保留待确认。全程不触碰对局数据（AC-3）。
    /// </summary>
    [TestFixture]
    public class PendingActionFeedbackTests
    {
        private static readonly StepOutcome AppliedNoSeq =
            new StepOutcome(StepApplyStatus.Applied, true, null, CommandError.None,
                string.Empty, Array.Empty<JsonValue>());

        private PendingCommandTracker _tracker = null!;
        private Dictionary<int, CardView> _cards = null!;
        private FloatingTextPool _texts = null!;
        private Transform _textParent = null!;
        private PendingActionFeedback _feedback = null!;
        private readonly List<Object> _trash = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _tracker = new PendingCommandTracker();
            _cards = new Dictionary<int, CardView>();
            _textParent = new GameObject("TextParent", typeof(RectTransform)).transform;
            FloatingTextView prefab = FeedbackTestPrefabs.CreateFloatingTextView();
            _trash.Add(prefab);
            _texts = new FloatingTextPool(prefab, _textParent);
            _feedback = new PendingActionFeedback(_tracker, Locate, _texts, _textParent);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _trash)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            Object.DestroyImmediate(_textParent.gameObject);
        }

        /// <summary>定位委托 stub：命中测试登记的卡牌视图。</summary>
        private bool Locate(int instanceId, out CardView? view)
        {
            return _cards.TryGetValue(instanceId, out view) && view != null;
        }

        private CardView RegisterCard(int instanceId)
        {
            CardView card = PresentationTestPrefabs.CreateCardViewPrefab();
            _trash.Add(card);
            card.SetData(new CardViewData("卡" + instanceId, "d", 1, 2, 3, "art", CardType.Minion, instanceId));
            _cards[instanceId] = card;
            return card;
        }

        private static StepOutcome Applied(int? seq)
        {
            return new StepOutcome(StepApplyStatus.Applied, true, seq, CommandError.None,
                string.Empty, Array.Empty<JsonValue>());
        }

        private static StepOutcome Rejected(int? seq, string detail)
        {
            return new StepOutcome(StepApplyStatus.Rejected, false, seq,
                CommandError.NotEnoughMana, detail, Array.Empty<JsonValue>());
        }

        [Test]
        public void NotifySubmitted_MarksCardPending_Immediately()
        {
            // AC-1 前半 + AC-3：上行即呈现待确认视觉（只改视觉，无状态写入面）
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(3, 11);

            Assert.That(_tracker.PendingCount, Is.EqualTo(1));
            CardPendingView pending = card.GetComponent<CardPendingView>();
            Assert.That(pending.IsPending, Is.True);
            Assert.That(card.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(CardPendingView.PendingAlpha));
        }

        [Test]
        public void AcceptedStep_ConfirmsPendingView_AndClearsTracker()
        {
            // AC-1 后半：接受步 → 确认复位（落位由状态同步驱动），pending 清空
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(3, 11);

            _feedback.NotifyStep(Applied(3));

            Assert.That(_tracker.PendingCount, Is.EqualTo(0));
            Assert.That(card.GetComponent<CardPendingView>().IsPending, Is.False);
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [Test]
        public void RejectedStep_RollsBackView_AndSpawnsReasonText()
        {
            // AC-2：拒绝步 → 回退视觉 + 卡牌位置浮出原因
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(5, 11);

            _feedback.NotifyStep(Rejected(5, "法力不足"));

            Assert.That(_tracker.PendingCount, Is.EqualTo(0));
            Assert.That(card.GetComponent<CardPendingView>().IsPending, Is.False);
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(_texts.ActiveCount, Is.EqualTo(1), "拒绝原因应产生一条浮动提示");
        }

        [Test]
        public void RejectedStep_WithoutDetail_ShowsErrorCodeName()
        {
            // 明细缺省回落错误码名（提示仍明确）
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(5, 11);

            _feedback.NotifyStep(Rejected(5, string.Empty));

            FloatingTextView text = _textParent.GetComponentInChildren<FloatingTextView>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text._text.text, Is.EqualTo(CommandError.NotEnoughMana.ToString()));
        }

        [Test]
        public void StepWithUnmatchedSeq_KeepsPendingVisual()
        {
            // AC-4：对手 step / seq 不匹配 → None，视觉不动
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(3, 11);

            _feedback.NotifyStep(Applied(4));

            Assert.That(_tracker.PendingCount, Is.EqualTo(1));
            Assert.That(card.GetComponent<CardPendingView>().IsPending, Is.True);
            Assert.That(_texts.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void NotifySubmitted_UnknownCardInstanceId_OnlyTracks()
        {
            // 定位未命中（如已离手）：仅记账，不标记、不抛异常
            _feedback.NotifySubmitted(3, 999);

            Assert.That(_tracker.PendingCount, Is.EqualTo(1));
            Assert.That(_texts.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateDiscardedStep_ConfirmsPending()
        {
            // AC-6：重复包（命令早已生效）→ 视同接受，清除待确认视觉
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(2, 11);
            StepOutcome duplicate = new StepOutcome(StepApplyStatus.DuplicateDiscarded, true, 2,
                CommandError.None, string.Empty, Array.Empty<JsonValue>());

            _feedback.NotifyStep(duplicate);

            Assert.That(_tracker.PendingCount, Is.EqualTo(0));
            Assert.That(card.GetComponent<CardPendingView>().IsPending, Is.False);
        }

        [Test]
        public void VersionGapStep_KeepsPendingVisual_UntilSnapshotReset()
        {
            // AC-7：版本空洞 → 视觉与 pending 都保留，由快照恢复方显式清理
            CardView card = RegisterCard(11);
            _feedback.NotifySubmitted(4, 11);
            StepOutcome gap = new StepOutcome(StepApplyStatus.VersionGap, true, 4,
                CommandError.None, string.Empty, Array.Empty<JsonValue>());

            _feedback.NotifyStep(gap);

            Assert.That(_tracker.PendingCount, Is.EqualTo(1));
            Assert.That(card.GetComponent<CardPendingView>().IsPending, Is.True);

            // 快照恢复路径：Clear 后重新同步（视觉由 SetData/重渲染复位）
            _tracker.Clear();
            Assert.That(_tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void NoSeqMode_FifoMatchesOldestVisual()
        {
            // AC-5：无序号模式，接受步 FIFO 命中最旧 pending 的可视记录
            CardView first = RegisterCard(21);
            RegisterCard(22);
            _feedback.NotifySubmitted(null, 21);
            _feedback.NotifySubmitted(null, 22);

            _feedback.NotifyStep(AppliedNoSeq);

            Assert.That(_tracker.PendingCount, Is.EqualTo(1));
            Assert.That(first.GetComponent<CardPendingView>().IsPending, Is.False,
                "最旧 pending 的可视应被本次接受步消费复位");
        }
    }
}
