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
        private ulong _stateVersion;

        public MatchHost(MatchController controller)
        {
            _controller = Guard.NotNull(controller, nameof(controller));
        }

        /// <summary>当前权威状态版本（每接受一条命令 +1）。</summary>
        public ulong StateVersion => _stateVersion;

        /// <summary>对局是否已结束（代理 MatchController.IsFinished）。</summary>
        public bool IsFinished => _controller.IsFinished;

        /// <summary>挂载一个客户端端点（宿主侧）。</summary>
        public void Attach(LoopbackEndpoint endpoint)
        {
            Guard.NotNull(endpoint, nameof(endpoint));
            _endpoints.Add(endpoint);
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
            // M11 identity：PlayerViewProjector 不裁，直接返回全量
            MatchState viewState = PlayerViewProjector.Project(_controller.State, observerPlayerId: 0);
            Send(endpoint, WriteSnapshot(viewState));
        }

        private void ProcessCommand(LoopbackEndpoint endpoint, JsonValue json)
        {
            // 读取命令
            IGameCommand command = ReadCommand(json);

            // before 副本 = 序列化往返（进程内模拟不追性能，M4-T10 已有此设计）
            MatchState before = MatchStateSerializer.Deserialize(
                MatchStateSerializer.Serialize(_controller.State));

            // 记录事件起点——EventLog 不暴露 Clear，用 Count 差值取本轮新事件
            int eventStart = _controller.Events.Count;

            CommandResult result = _controller.Submit(command);
            bool accepted = result.IsValid;
            CommandError error = result.Error;
            bool finished = _controller.IsFinished;

            IReadOnlyList<StateChange> changes;
            IReadOnlyList<GameEvent> events;

            if (accepted)
            {
                _stateVersion++;
                changes = MatchStateDiffer.Diff(before, _controller.State);
                int eventEnd = _controller.Events.Count;
                List<GameEvent> roundEvents = new List<GameEvent>(eventEnd - eventStart);
                for (int i = eventStart; i < eventEnd; i++)
                {
                    roundEvents.Add(_controller.Events[i]);
                }

                events = roundEvents;
            }
            else
            {
                changes = Array.Empty<StateChange>();
                events = Array.Empty<GameEvent>();
            }

            Broadcast(WriteStep(accepted, error, finished, changes, events));
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

        private static IGameCommand ReadCommand(JsonValue json)
        {
            if (!json.TryGetMember("payload", out JsonValue payload))
            {
                throw new FormatException("command 消息缺少 payload 字段。");
            }

            return CommandSerializer.Deserialize(payload.ToJson());
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
            bool finished,
            IReadOnlyList<StateChange> changes,
            IReadOnlyList<GameEvent> events)
        {
            return JsonValue.Object()
                .Add("kind", JsonValue.From("step"))
                .Add("version", JsonValue.From((int)_stateVersion))
                .Add("accepted", JsonValue.From(accepted))
                .Add("error", JsonValue.From(error.ToString()))
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
