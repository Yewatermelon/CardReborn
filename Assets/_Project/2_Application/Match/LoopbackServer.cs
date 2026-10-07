using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 进程内回环权威服务器（M4-T10）：持有唯一 MatchController，接收客户端上行
    /// （hello / command），校验/结算后向所有 Attach 的端点广播 step（含增量），
    /// hello 只回请求者快照。单线程 Pump 驱动，无网络/线程。
    /// 状态版本 <see cref="Version"/>：每接受一条命令 +1，拒绝不变。
    /// </summary>
    public sealed class LoopbackServer
    {
        private readonly MatchController _controller;
        private readonly List<LoopbackEndpoint> _endpoints = new List<LoopbackEndpoint>();
        private int _version;

        public LoopbackServer(MatchController controller)
        {
            _controller = Guard.NotNull(controller, nameof(controller));
        }

        /// <summary>当前状态版本（每接受一条命令 +1）。</summary>
        public int Version => _version;

        /// <summary>挂载一个客户端端点（服务器侧）。</summary>
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

        private void ProcessOne(LoopbackEndpoint endpoint, string raw)
        {
            JsonValue json = JsonValue.Parse(raw);

            if (LoopbackProtocol.TryReadHello(json))
            {
                Send(endpoint, LoopbackProtocol.WriteSnapshot(_version, _controller.State));
                return;
            }

            IGameCommand command = LoopbackProtocol.ReadCommand(json);

            // before 副本 = 序列化往返（进程内模拟不追性能，见任务卡 Q2）
            MatchState before = MatchStateSerializer.Deserialize(
                MatchStateSerializer.Serialize(_controller.State));

            CommandResult result = _controller.Submit(command);
            bool accepted = result.IsValid;
            CommandError error = result.Error;
            bool finished = _controller.IsFinished;

            IReadOnlyList<StateChange> changes;
            if (accepted)
            {
                _version++;
                changes = MatchStateDiffer.Diff(before, _controller.State);
            }
            else
            {
                changes = Array.Empty<StateChange>();
            }

            Broadcast(LoopbackProtocol.WriteStep(_version, accepted, error, finished, changes));
        }

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
