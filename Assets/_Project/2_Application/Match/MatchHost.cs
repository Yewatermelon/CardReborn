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
    /// </summary>
    public sealed class MatchHost : IMatchHost
    {
        private readonly MatchController _controller;
        private readonly List<LoopbackEndpoint> _endpoints = new List<LoopbackEndpoint>();
        private readonly Dictionary<LoopbackEndpoint, int> _seats = new Dictionary<LoopbackEndpoint, int>();
        private readonly List<int> _attachedSeats = new List<int>();
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
            string kind = ReadKind(json);

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
            // M13-T1：按端点座位投影（M11 identity 实现，M13-T6 落地真实裁剪）
            MatchState viewState = PlayerViewProjector.Project(_controller.State, _seats[endpoint]);
            Send(endpoint, WriteSnapshot(viewState));
        }

        private void ProcessCommand(LoopbackEndpoint endpoint, JsonValue json)
        {
            // M13-T2 信封级校验 ①：载荷可解析——畸形命令产出结构化拒绝，不抛异常打死宿主
            if (!TryReadCommand(json, out IGameCommand? command, out string? readError))
            {
                Reject(CommandError.UnknownCommand, readError!);
                return;
            }

            // M13-T2 信封级校验 ②：座位归属——命令归属玩家必须与端点绑定座位一致
            int seat = _seats[endpoint];
            if (command!.PlayerId != seat)
            {
                Reject(CommandError.NotYourTurn,
                    "命令归属玩家 " + command.PlayerId + " 与端点座位 " + seat + " 不匹配。");
                return;
            }

            SubmitAndBroadcast(command);
        }

        /// <summary>提交命令给权威并广播 step（增量 + 事件 / 拒绝原因）。</summary>
        private void SubmitAndBroadcast(IGameCommand command)
        {
            // before 副本 = 序列化往返（进程内模拟不追性能，M4-T10 已有此设计）
            MatchState before = MatchStateSerializer.Deserialize(
                MatchStateSerializer.Serialize(_controller.State));

            // 记录事件起点——EventLog 不暴露 Clear，用 Count 差值取本轮新事件
            int eventStart = _controller.Events.Count;

            CommandResult result = _controller.Submit(command);
            bool accepted = result.IsValid;
            bool finished = _controller.IsFinished;

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

            Broadcast(WriteStep(accepted, result.Error, result.Detail, finished, changes, events));
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

        /// <summary>广播一条结构化拒绝 step（M13-T2）：状态零变更，拒绝原因可观测。</summary>
        private void Reject(CommandError error, string detail)
        {
            Broadcast(WriteStep(
                accepted: false,
                error,
                detail,
                finished: _controller.IsFinished,
                Array.Empty<StateChange>(),
                Array.Empty<GameEvent>()));
        }

        // -----------------------------------------------------------------
        // 私有：消息编解码（替代原 LoopbackProtocol）
        // -----------------------------------------------------------------

        private static string ReadKind(JsonValue json)
        {
            if (json.TryGetMember("kind", out JsonValue kind) && kind.Kind == JsonKind.String)
            {
                return kind.StringValue;
            }

            throw new FormatException("上行消息缺少 kind 字段。");
        }

        /// <summary>
        /// 读取 command 消息载荷（M13-T2）：缺 payload / 非法 JSON / 未知类型 / 缺成员
        /// 一律返回 false 并携带可读原因，由调用方产出结构化拒绝。
        /// </summary>
        private static bool TryReadCommand(JsonValue json, out IGameCommand? command, out string? error)
        {
            if (!json.TryGetMember("payload", out JsonValue payload))
            {
                command = null;
                error = "command 消息缺少 payload 字段。";
                return false;
            }

            try
            {
                command = CommandSerializer.Deserialize(payload.ToJson());
                error = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                command = null;
                error = "命令载荷非法：" + ex.Message;
                return false;
            }
            catch (FormatException ex)
            {
                command = null;
                error = "命令载荷不是合法 JSON：" + ex.Message;
                return false;
            }
        }

        private static JsonValue WriteSnapshot(MatchState state)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("snapshot"))
                .Add("state", MatchStateSerializer.WriteStateTree(state));
        }

        private JsonValue WriteStep(
            bool accepted,
            CommandError error,
            string detail,
            bool finished,
            IReadOnlyList<StateChange> changes,
            IReadOnlyList<GameEvent> events)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("step"))
                .Add("version", JsonValue.From((int)_stateVersion))
                .Add("accepted", JsonValue.From(accepted))
                .Add("error", JsonValue.From(error.ToString()))
                .Add("detail", JsonValue.From(detail))
                .Add("finished", JsonValue.From(finished))
                .Add("changes", StateChangeSerializer.WriteList(changes))
                .Add("events", JsonValue.Array(SerializeEvents(events)));
        }

        private static JsonValue[] SerializeEvents(IReadOnlyList<GameEvent> events)
        {
            JsonValue[] items = new JsonValue[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                items[i] = EventSerializer.WriteEvent(events[i]);
            }

            return items;
        }

        // -----------------------------------------------------------------
        // 私有：端点收发
        // -----------------------------------------------------------------

        private void Send(LoopbackEndpoint endpoint, JsonValue message)
        {
            endpoint.Send(message.ToJson());
        }

        private void Broadcast(JsonValue message)
        {
            string raw = message.ToJson();
            for (int i = 0; i < _endpoints.Count; i++)
            {
                _endpoints[i].Send(raw);
            }
        }
    }
}
