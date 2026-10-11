using System;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// 在途命令追踪器测试（M14-T1，Docs/05 §6.1 表现先行）：
    /// seq 精确匹配 / 无 seq FIFO / 混合不猜 / 重复包视同接受 / VersionGap 保留 / Clear。
    /// </summary>
    [TestFixture]
    public class PendingCommandTrackerTests
    {
        private static StepOutcome Applied(int? seq)
        {
            return new StepOutcome(StepApplyStatus.Applied, true, seq,
                CommandError.None, string.Empty, Array.Empty<JsonValue>());
        }

        private static StepOutcome Rejected(int? seq, CommandError error, string detail)
        {
            return new StepOutcome(StepApplyStatus.Rejected, false, seq,
                error, detail, Array.Empty<JsonValue>());
        }

        [Test]
        public void Resolve_WithoutPending_ReturnsNone()
        {
            // AC-9：空 pending 任意 step → None，不抛异常
            PendingCommandTracker tracker = new PendingCommandTracker();
            Assert.That(tracker.Resolve(Applied(null)).Kind, Is.EqualTo(PendingResolutionKind.None));
            Assert.That(tracker.Resolve(Rejected(1, CommandError.NotYourTurn, "x")).Kind,
                Is.EqualTo(PendingResolutionKind.None));
        }

        [Test]
        public void TrackNoSeq_NextStepResolvesOldestAsAccepted()
        {
            // AC-5：无序号模式，下一个 step 按 FIFO 命中最旧 pending
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(null);
            tracker.Track(null);
            Assert.That(tracker.PendingCount, Is.EqualTo(2));

            Assert.That(tracker.Resolve(Applied(null)).Kind, Is.EqualTo(PendingResolutionKind.Accepted));
            Assert.That(tracker.Resolve(Applied(null)).Kind, Is.EqualTo(PendingResolutionKind.Accepted));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void TrackSeq_AcceptedStepWithSameSeq_ResolvesAcceptedAndClears()
        {
            // AC-1：seq=3 登记后权威接受步 seq=3 → Accepted 且 pending 清空
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(3);
            PendingResolution resolution = tracker.Resolve(Applied(3));
            Assert.That(resolution.Kind, Is.EqualTo(PendingResolutionKind.Accepted));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void TrackSeq_StepWithDifferentSeq_ReturnsNoneAndKeepsPending()
        {
            // AC-4：seq 不匹配（对手 step / 未登记命令）→ None，pending 保留
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(3);
            Assert.That(tracker.Resolve(Applied(4)).Kind, Is.EqualTo(PendingResolutionKind.None));
            Assert.That(tracker.Resolve(Rejected(5, CommandError.NotYourTurn, "x")).Kind,
                Is.EqualTo(PendingResolutionKind.None));
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectedStep_CarriesErrorAndDetail_AndClearsPending()
        {
            // AC-2（tracker 部分）：拒绝步携带错误码与明细，pending 移除
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(7);
            PendingResolution resolution = tracker.Resolve(
                Rejected(7, CommandError.NotEnoughMana, "法力不足"));
            Assert.That(resolution.Kind, Is.EqualTo(PendingResolutionKind.Rejected));
            Assert.That(resolution.Error, Is.EqualTo(CommandError.NotEnoughMana));
            Assert.That(resolution.Detail, Is.EqualTo("法力不足"));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateDiscarded_MatchingPending_ResolvesAccepted()
        {
            // AC-6：重复包（命令早已生效）命中 pending → 视同 Accepted 清除
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(2);
            StepOutcome duplicate = new StepOutcome(StepApplyStatus.DuplicateDiscarded, true, 2,
                CommandError.None, string.Empty, Array.Empty<JsonValue>());
            Assert.That(tracker.Resolve(duplicate).Kind, Is.EqualTo(PendingResolutionKind.Accepted));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void VersionGap_KeepsPending_ClearEmpties()
        {
            // AC-7：版本空洞不猜——pending 保留，由快照恢复方显式 Clear
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(4);
            StepOutcome gap = new StepOutcome(StepApplyStatus.VersionGap, true, 4,
                CommandError.None, string.Empty, Array.Empty<JsonValue>());
            Assert.That(tracker.Resolve(gap).Kind, Is.EqualTo(PendingResolutionKind.None));
            Assert.That(tracker.PendingCount, Is.EqualTo(1));

            tracker.Clear();
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void MixedMode_PendingWithSeqButStepWithoutSeq_ReturnsNone()
        {
            // 混合模式不猜：队头带 seq 而步缺 seq → None 且不动（防御协议错配）
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(9);
            Assert.That(tracker.Resolve(Applied(null)).Kind, Is.EqualTo(PendingResolutionKind.None));
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void MultiplePending_FifoResolvedInTrackOrder()
        {
            // 多条在途：接受步逐条按登记顺序解析（seq 模式按各自 seq 对号）
            PendingCommandTracker tracker = new PendingCommandTracker();
            tracker.Track(1);
            tracker.Track(2);
            tracker.Track(3);

            Assert.That(tracker.Resolve(Rejected(2, CommandError.BoardFull, "场满")).Kind,
                Is.EqualTo(PendingResolutionKind.Rejected));
            Assert.That(tracker.Resolve(Applied(1)).Kind, Is.EqualTo(PendingResolutionKind.Accepted));
            Assert.That(tracker.Resolve(Applied(3)).Kind, Is.EqualTo(PendingResolutionKind.Accepted));
            Assert.That(tracker.PendingCount, Is.EqualTo(0));
        }
    }
}
