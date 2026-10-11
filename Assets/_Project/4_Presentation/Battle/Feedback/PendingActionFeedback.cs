using System;
using System.Collections.Generic;
using Card.Application.Match;
using Card.Core;
using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>按实例 Id 查卡牌视图的定位委托（装配方桥接 HandView/BoardView）。</summary>
    public delegate bool CardViewLocator(int instanceId, out CardView? view);

    /// <summary>
    /// 表现先行协调器（M14-T1，Docs/05 §6.1 基础版本）：
    /// 上行时 <see cref="NotifySubmitted"/> 登记在途命令并对卡牌施加待确认视觉；
    /// 权威 step 到达时 <see cref="NotifyStep"/> 经 <see cref="PendingCommandTracker"/>
    /// 裁决——接受则确认复位（落位由状态同步驱动）、拒绝则回退并在卡牌位置浮出原因。
    /// 只驱动视觉，不触碰任何对局状态；无定位器/文字池时退化为纯 tracker 记账。
    /// </summary>
    public sealed class PendingActionFeedback
    {
        private const float ReasonDurationSeconds = 1.2f;
        private static readonly Color ReasonColor = new Color(1f, 0.35f, 0.25f);

        private readonly PendingCommandTracker _tracker;
        private readonly CardViewLocator? _locator;
        private readonly FloatingTextPool? _texts;
        private readonly Transform? _textParent;
        private readonly List<PendingVisual> _visuals = new List<PendingVisual>();

        /// <summary>与 tracker 登记顺序平行的可视记录（seq 用于与裁决步对号）。</summary>
        private readonly struct PendingVisual
        {
            public PendingVisual(int? seq, int? cardInstanceId)
            {
                Seq = seq;
                CardInstanceId = cardInstanceId;
            }

            public int? Seq { get; }
            public int? CardInstanceId { get; }
        }

        /// <summary>
        /// 构造协调器；locator/texts/textParent 可空（退化模式，仅记账）。
        /// </summary>
        public PendingActionFeedback(PendingCommandTracker tracker,
            CardViewLocator? locator, FloatingTextPool? texts, Transform? textParent)
        {
            _tracker = Guard.NotNull(tracker, nameof(tracker));
            _locator = locator;
            _texts = texts;
            _textParent = textParent;
        }

        /// <summary>
        /// 命令上行时调用：登记 pending；携带卡牌实例 Id 时对该卡施加待确认视觉。
        /// </summary>
        public void NotifySubmitted(int? seq, int? cardInstanceId)
        {
            _tracker.Track(seq);
            _visuals.Add(new PendingVisual(seq, cardInstanceId));

            if (cardInstanceId.HasValue && TryLocate(cardInstanceId.Value, out CardView? view))
            {
                GetPendingView(view!).ShowPending();
            }
        }

        /// <summary>权威 step 到达时调用：裁决并驱动确认/回退视觉与拒绝提示。</summary>
        public void NotifyStep(StepOutcome outcome)
        {
            PendingResolution resolution = _tracker.Resolve(outcome);
            if (resolution.Kind == PendingResolutionKind.None)
            {
                return;
            }

            int? cardInstanceId = TakeVisualCardId(outcome.Seq);
            if (resolution.Kind == PendingResolutionKind.Accepted)
            {
                if (TryLocate(cardInstanceId, out CardView? view))
                {
                    GetPendingView(view!).Confirm();
                }

                return;
            }

            if (TryLocate(cardInstanceId, out CardView? rejectedView))
            {
                GetPendingView(rejectedView!).Rollback();
                ShowReason(rejectedView!, resolution);
            }

            GameLog.Warn(LogChannel.Ui, "命令被权威侧拒绝：" + resolution.Error
                + (string.IsNullOrEmpty(resolution.Detail) ? string.Empty : "（" + resolution.Detail + "）"));
        }

        /// <summary>
        /// 快照重置恢复后调用（M14-T3 接线）：清空在途登记与待确认视觉——
        /// 落位以恢复的快照为准（Confirm 复位，不回退）。
        /// </summary>
        public void ClearPending()
        {
            _tracker.Clear();
            for (int i = 0; i < _visuals.Count; i++)
            {
                if (TryLocate(_visuals[i].CardInstanceId, out CardView? view))
                {
                    GetPendingView(view!).Confirm();
                }
            }

            _visuals.Clear();
        }

        private int? TakeVisualCardId(int? resolvedSeq)
        {
            for (int i = 0; i < _visuals.Count; i++)
            {
                if (_visuals[i].Seq == resolvedSeq)
                {
                    int? cardInstanceId = _visuals[i].CardInstanceId;
                    _visuals.RemoveAt(i);
                    return cardInstanceId;
                }
            }

            return null;
        }

        private bool TryLocate(int? cardInstanceId, out CardView? view)
        {
            view = null;
            if (!cardInstanceId.HasValue || _locator == null
                || !_locator(cardInstanceId.Value, out CardView? found) || found == null)
            {
                return false;
            }

            view = found;
            return true;
        }

        private void ShowReason(CardView view, PendingResolution resolution)
        {
            if (_texts == null || _textParent == null)
            {
                return;
            }

            FloatingTextView text = _texts.Rent(_textParent);
            text.transform.position = view.transform.position;
            text.Show(string.IsNullOrEmpty(resolution.Detail)
                ? resolution.Error.ToString()
                : resolution.Detail, ReasonColor, ReasonDurationSeconds);
        }

        private static CardPendingView GetPendingView(CardView view)
        {
            if (!view.TryGetComponent(out CardPendingView pending))
            {
                pending = view.gameObject.AddComponent<CardPendingView>();
            }

            return pending;
        }
    }
}
