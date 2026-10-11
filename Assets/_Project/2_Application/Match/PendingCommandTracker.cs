using System.Collections.Generic;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>在途命令裁决种类（M14-T1，Docs/05 §6.1 表现先行）。</summary>
    public enum PendingResolutionKind
    {
        /// <summary>无匹配（对手 step、未登记命令、版本空洞）——表现层忽略。</summary>
        None = 0,

        /// <summary>权威接受该命令——清除待确认视觉，落位交由状态同步。</summary>
        Accepted = 1,

        /// <summary>权威拒绝该命令——回退视觉并提示 <see cref="PendingResolution.Error"/>/<see cref="PendingResolution.Detail"/>。</summary>
        Rejected = 2
    }

    /// <summary>在途命令裁决结果：种类 + 拒绝原因（仅 Rejected 有效）。</summary>
    public sealed class PendingResolution
    {
        private static readonly PendingResolution NoneInstance =
            new PendingResolution(PendingResolutionKind.None, CommandError.None, string.Empty);

        private static readonly PendingResolution AcceptedInstance =
            new PendingResolution(PendingResolutionKind.Accepted, CommandError.None, string.Empty);

        private PendingResolution(PendingResolutionKind kind, CommandError error, string detail)
        {
            Kind = kind;
            Error = error;
            Detail = detail ?? string.Empty;
        }

        /// <summary>无匹配单例。</summary>
        public static PendingResolution None => NoneInstance;

        /// <summary>接受单例。</summary>
        public static PendingResolution Accepted => AcceptedInstance;

        /// <summary>构造拒绝结果（携带错误码与可读明细）。</summary>
        public static PendingResolution Rejected(CommandError error, string detail)
        {
            return new PendingResolution(PendingResolutionKind.Rejected, error, detail);
        }

        /// <summary>裁决种类。</summary>
        public PendingResolutionKind Kind { get; }

        /// <summary>拒绝错误码（仅 Rejected 有效）。</summary>
        public CommandError Error { get; }

        /// <summary>拒绝明细（仅 Rejected 有效；权威未给时为空串）。</summary>
        public string Detail { get; }
    }

    /// <summary>
    /// 在途命令追踪器（M14-T1）：上行时 <see cref="Track"/> 登记，权威 step 到达时
    /// <see cref="Resolve"/> 裁决。纯 BCL，不触碰任何对局状态——"表现先行不改本地状态"
    /// 由结构保证（Docs/05 §6.1）；落位交由 <see cref="ClientStateSync"/> 的权威增量。
    /// 匹配规则：步带 seq → 与 pending 按 seq 精确匹配；步缺 seq → 队头 pending 也缺 seq
    /// 时按 FIFO 命中最旧；混合（队头带 seq 而步缺 seq）不猜，返回 None。
    /// VersionGap 返回 None 且保留 pending（快照恢复后由恢复方 <see cref="Clear"/>）。
    /// </summary>
    public sealed class PendingCommandTracker
    {
        private readonly List<int?> _pendingSeqs = new List<int?>();

        /// <summary>当前在途命令数。</summary>
        public int PendingCount => _pendingSeqs.Count;

        /// <summary>上行时登记在途命令（seq 缺省 = 无序号模式，记 null）。</summary>
        public void Track(int? seq)
        {
            _pendingSeqs.Add(seq);
        }

        /// <summary>
        /// 权威步到达时裁决：命中 pending → Accepted（含重复包）/ Rejected 并移除；
        /// 无匹配或版本空洞 → None 且 pending 不动。
        /// </summary>
        public PendingResolution Resolve(StepOutcome outcome)
        {
            // 版本空洞不猜：命令是否已在权威侧生效未知，pending 保留待快照恢复后 Clear。
            if (outcome.Status == StepApplyStatus.VersionGap)
            {
                return PendingResolution.None;
            }

            int index = MatchIndex(outcome);
            if (index < 0)
            {
                return PendingResolution.None;
            }

            _pendingSeqs.RemoveAt(index);
            return outcome.Accepted
                ? PendingResolution.Accepted
                : PendingResolution.Rejected(outcome.Error, outcome.Detail);
        }

        /// <summary>清空全部在途记录（快照重置 / 断线重连时调用）。</summary>
        public void Clear()
        {
            _pendingSeqs.Clear();
        }

        /// <summary>按匹配规则定位命中的 pending 下标；未命中返回 -1。</summary>
        private int MatchIndex(StepOutcome outcome)
        {
            if (outcome.Seq.HasValue)
            {
                for (int i = 0; i < _pendingSeqs.Count; i++)
                {
                    if (_pendingSeqs[i].HasValue && _pendingSeqs[i]!.Value == outcome.Seq.Value)
                    {
                        return i;
                    }
                }

                return -1;
            }

            if (_pendingSeqs.Count > 0 && !_pendingSeqs[0].HasValue)
            {
                return 0;
            }

            return -1;
        }
    }
}
