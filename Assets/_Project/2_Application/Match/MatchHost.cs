using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 权威对局运行时（M11-T2，提炼自 M4-T10 LoopbackServer）：持有唯一 MatchController，
    /// 接收客户端上行（hello / command via envelope），校验/结算后向所有 Attach 的端点广播
    /// step（含增量 + 事件）。单线程 Pump 驱动，无网络/线程。
    /// 状态版本 <see cref="StateVersion"/>：每接受一条命令 +1，拒绝不变（ulong 防长时间溢出）。
    /// 命令序号与幂等（M13-T3）：信封 seq 按座位独立裁决（<see cref="SeatCommandInbox"/>），
    /// 重复包只执行一次并幂等重放回执，过期/冲突序号结构化拒绝；编解码见 <see cref="MatchHostProtocol"/>。
    /// </summary>
    public sealed class MatchHost : IMatchHost
    {
        private readonly MatchController _controller;
        private readonly List<LoopbackEndpoint> _endpoints = new List<LoopbackEndpoint>();
        private readonly Dictionary<LoopbackEndpoint, int> _seats = new Dictionary<LoopbackEndpoint, int>();
        private readonly List<int> _attachedSeats = new List<int>();
        private readonly Dictionary<int, SeatCommandInbox> _inboxes = new Dictionary<int, SeatCommandInbox>();
        private ulong _stateVersion;

        public MatchHost(MatchController controller)
        {
            _controller = Guard.NotNull(controller, nameof(controller));
        }

        /// <summary>当前权威状态版本（每接受一条命令 +1）。</summary>
        public ulong StateVersion => _stateVersion;

        /// <summary>对局是否已结束（代理 MatchController.IsFinished）。</summary>
        public bool IsFinished => _controller.IsFinished;

        /// <summary>已挂载端点的座位列表（M13-T1：快照按座位投影）。</summary>
        public IReadOnlyList<int> AttachedSeats => _attachedSeats;

        /// <summary>挂载一个客户端端点（宿主侧），默认座位 0（M11 兼容）。</summary>
        public void Attach(LoopbackEndpoint endpoint)
        {
            Attach(endpoint, 0);
        }

        /// <summary>挂载一个客户端端点并绑定座位 Id（M13-T1：投影与裁剪按座位）。</summary>
        public void Attach(LoopbackEndpoint endpoint, int seatPlayerId)
        {
            Guard.NotNull(endpoint, nameof(endpoint));
            _endpoints.Add(endpoint);
            _seats[endpoint] = seatPlayerId;
            _attachedSeats.Add(seatPlayerId);
        }

        /// <summary>处理全部端点的待收上行消息；返回处理条数。</summary>
        public int Pump()
        {
            int processed = 0;
            for (int i = 0; i < _endpoints.Count; i++)
            {
                LoopbackEndpoint ep = _endpoints[i];
                while (ep.TryReceive(out string raw))
                {
                    ProcessOne(ep, raw);
                    processed++;
                }
            }

            return processed;
        }

        // -----------------------------------------------------------------
        // 私有：消息处理
        // -----------------------------------------------------------------

        private void ProcessOne(LoopbackEndpoint endpoint, string raw)
        {
            JsonValue json = JsonValue.Parse(raw);
            string kind = MatchHostProtocol.ReadKind(json);

            if (kind == "hello")
            {
                ProcessHello(endpoint);
                return;
            }

            if (kind == "command")
            {
                ProcessCommand(endpoint, json);
                return;
            }

            throw new FormatException("未知上行消息 kind：\"" + kind + "\"。");
        }

        private void ProcessHello(LoopbackEndpoint endpoint)
        {
            // M13-T1：按端点座位投影（M11 identity 实现，M13-T6 落地真实裁剪）；
            // M13-T4：快照携带当前权威版本，作为客户端应用增量的基线
            MatchState viewState = PlayerViewProjector.Project(_controller.State, _seats[endpoint]);
            Send(endpoint, MatchHostProtocol.WriteSnapshot(viewState, _stateVersion));
        }

        private void ProcessCommand(LoopbackEndpoint endpoint, JsonValue json)
        {
            // M13-T2 信封级校验 ①：载荷可解析——畸形命令产出结构化拒绝，不抛异常打死宿主
            if (!MatchHostProtocol.TryReadCommand(json, out IGameCommand? command, out string? readError))
            {
                Reject(CommandError.UnknownCommand, readError!);
                return;
            }

            // M13-T2 信封级校验 ②：座位归属——不消费 seq（冒用他人身份不推进自己收件箱）
            int seat = _seats[endpoint];
            if (command!.PlayerId != seat)
            {
                Reject(CommandError.NotYourTurn,
                    "命令归属玩家 " + command.PlayerId + " 与端点座位 " + seat + " 不匹配。");
                return;
            }

            // M13-T3 信封级校验 ③：seq 缺省 = 无序号模式（兼容 M11/T2 客户端）；非法值按信封畸形拒绝
            if (!TryReadSeq(json, out int? seq))
            {
                Reject(CommandError.UnknownCommand, "command 信封 seq 字段非法（应为 ≥1 的整数）。");
                return;
            }

            SeatCommandInbox? inbox = null;
            if (seq.HasValue)
            {
                inbox = GetInbox(seat);
                if (HandleSequenced(endpoint, inbox, seq.Value, json))
                {
                    return;
                }
            }

            SubmitAndBroadcast(inbox, seq, command, json);
        }

        /// <summary>M13-T3 序号幂等裁决：返回 true 表示已处理（拒绝或幂等重放），无需再结算。</summary>
        private bool HandleSequenced(LoopbackEndpoint endpoint, SeatCommandInbox inbox, int seq, JsonValue json)
        {
            if (inbox.IsExecutable(seq))
            {
                return false;
            }

            if (seq == inbox.LastSeq && inbox.MatchesLastPayload(ExtractPayloadJson(json)))
            {
                endpoint.Send(inbox.LastStepJson);   // 幂等回执：不执行、不推进版本
                return true;
            }

            string why = seq < inbox.LastSeq
                ? "已过期（该座位最新已消费 " + inbox.LastSeq + "）"
                : "已被其他命令使用";
            Reject(CommandError.StaleSequence, "命令序号 " + seq + " " + why + "。", seq);
            return true;
        }

        /// <summary>提交命令给权威并广播 step（StateDelta：basedOn + version + 变化 + 事件）；带序号时记录回执。</summary>
        private void SubmitAndBroadcast(SeatCommandInbox? inbox, int? seq, IGameCommand command, JsonValue envelope)
        {
            ulong basedOn = _stateVersion; // 增量基线 = 结算前版本（M13-T4）

            // before 副本 = 序列化往返（进程内模拟不追性能，M4-T10 已有此设计）
            MatchState before = MatchStateSerializer.Deserialize(
                MatchStateSerializer.Serialize(_controller.State));

            // 记录事件起点——EventLog 不暴露 Clear，用 Count 差值取本轮新事件
            int eventStart = _controller.Events.Count;

            CommandResult result = _controller.Submit(command);
            bool accepted = result.IsValid;

            IReadOnlyList<StateChange> changes;
            IReadOnlyList<GameEvent> events;

            if (accepted)
            {
                _stateVersion++;
                changes = MatchStateDiffer.Diff(before, _controller.State);
                events = CollectNewEvents(eventStart);
            }
            else
            {
                changes = Array.Empty<StateChange>();
                events = Array.Empty<GameEvent>();
            }

            string stepJson = MatchHostProtocol.WriteStep(
                    _stateVersion, basedOn, seq, accepted, result.Error, result.Detail,
                    _controller.IsFinished, changes, events)
                .ToJson();

            if (inbox != null && seq.HasValue)
            {
                inbox.Record(seq.Value, ExtractPayloadJson(envelope), stepJson);
            }

            Broadcast(stepJson);
        }

        /// <summary>按事件起点截取本轮新增事件（EventLog 只追加）。</summary>
        private List<GameEvent> CollectNewEvents(int eventStart)
        {
            int eventEnd = _controller.Events.Count;
            List<GameEvent> roundEvents = new List<GameEvent>(eventEnd - eventStart);
            for (int i = eventStart; i < eventEnd; i++)
            {
                roundEvents.Add(_controller.Events[i]);
            }

            return roundEvents;
        }

        /// <summary>广播一条结构化拒绝 step（M13-T2）：状态零变更，拒绝原因可观测；basedOn = 当前版本（未推进）。</summary>
        private void Reject(CommandError error, string detail, int? seq = null)
        {
            string stepJson = MatchHostProtocol.WriteStep(
                    _stateVersion, _stateVersion, seq, accepted: false, error, detail,
                    finished: _controller.IsFinished,
                    Array.Empty<StateChange>(), Array.Empty<GameEvent>())
                .ToJson();
            Broadcast(stepJson);
        }

        // -----------------------------------------------------------------
        // 私有：信封 seq 与收件箱（M13-T3）
        // -----------------------------------------------------------------

        /// <summary>读取信封 seq：缺省 = 无序号模式（true + null）；存在但非 ≥1 整数 = false。</summary>
        private static bool TryReadSeq(JsonValue json, out int? seq)
        {
            if (!json.TryGetMember("seq", out JsonValue value))
            {
                seq = null;
                return true;
            }

            if (value.Kind == JsonKind.Number && value.IntValue >= 1)
            {
                seq = value.IntValue;
                return true;
            }

            seq = null;
            return false;
        }

        /// <summary>提取 payload 原文（幂等比对键：载荷逐字节一致才算重复包）。</summary>
        private static string ExtractPayloadJson(JsonValue envelope)
        {
            return envelope.TryGetMember("payload", out JsonValue payload)
                ? payload.ToJson()
                : string.Empty;
        }

        private SeatCommandInbox GetInbox(int seat)
        {
            if (!_inboxes.TryGetValue(seat, out SeatCommandInbox? inbox))
            {
                inbox = new SeatCommandInbox();
                _inboxes[seat] = inbox;
            }

            return inbox;
        }

        // -----------------------------------------------------------------
        // 私有：端点收发
        // -----------------------------------------------------------------

        private void Send(LoopbackEndpoint endpoint, JsonValue message)
        {
            endpoint.Send(message.ToJson());
        }

        private void Broadcast(string raw)
        {
            for (int i = 0; i < _endpoints.Count; i++)
            {
                _endpoints[i].Send(raw);
            }
        }
    }
}
