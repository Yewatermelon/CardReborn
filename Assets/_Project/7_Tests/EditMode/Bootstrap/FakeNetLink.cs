using System;
using System.Collections.Generic;
using Card.Bootstrap.Battle.Net;
using Card.Core;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// 座位链路 fake（M14-T3 测试夹具）：手动注入下行快照/增量、记录上行信封与 Ready 次数。
    /// 供 PvpCommandSink / PvpLobbyFlow 状态机测试驱动，不经过真实网络。
    /// </summary>
    internal sealed class FakeNetLink : INetMatchLink
    {
        public bool Alive { get; set; } = true;

        public bool MatchStarted { get; set; }

        public readonly List<string> Uplinks = new List<string>();

        public readonly Queue<JsonValue> Downlink = new Queue<JsonValue>();

        public int ReadyCount { get; private set; }

        public int DisposeCount { get; private set; }

        public event Action<int?, string>? MatchEnded;

        public void Pump(Action<JsonValue> onDownlink)
        {
            while (Downlink.Count > 0)
            {
                onDownlink(Downlink.Dequeue());
            }
        }

        public void SendUplink(string envelopeJson)
        {
            Uplinks.Add(envelopeJson);
        }

        public void SendReady()
        {
            ReadyCount++;
        }

        public void RaiseMatchEnded(int? winner, string reason)
        {
            MatchEnded?.Invoke(winner, reason);
        }

        public void Dispose()
        {
            DisposeCount++;
            Alive = false;
        }
    }
}
