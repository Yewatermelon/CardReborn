using System;
using System.Collections.Generic;
using Card.Core;
using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 进程内回环只读客户端（M4-T10）：上行 hello / command，下行快照 / step。
    /// 视图 <see cref="View"/> 为只读语义（调用方不得修改），未收到快照前为 null。
    /// 未 Attach / 未连接时 SubmitCommand 抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    public sealed class LoopbackClient
    {
        private LoopbackEndpoint? _endpoint;
        private MatchState? _view;
        private bool _isConnected;
        private int _version;
        private bool _lastAccepted;
        private CommandError _lastError;

        /// <summary>当前只读视图；未收到快照为 null。</summary>
        public MatchState? View => _view;

        /// <summary>是否已收到服务器快照（连接建立）。</summary>
        public bool IsConnected => _isConnected;

        /// <summary>当前视图版本（与服务器 Version 同步）。</summary>
        public int Version => _version;

        /// <summary>最近一条上行命令是否被接受。</summary>
        public bool LastAccepted => _lastAccepted;

        /// <summary>最近一条上行命令的拒绝原因；接受时为 <see cref="CommandError.None"/>。</summary>
        public CommandError LastError => _lastError;

        /// <summary>挂载到服务器端点（客户端侧）。</summary>
        public void Attach(LoopbackEndpoint endpoint)
        {
            Guard.NotNull(endpoint, nameof(endpoint));
            _endpoint = endpoint;
        }

        /// <summary>向服务器请求快照（上行 hello）。</summary>
        public void RequestSnapshot()
        {
            EnsureAttached();
            _endpoint!.Send(LoopbackProtocol.WriteHello().ToJson());
        }

        /// <summary>上行一条命令（序列化后经通道发送）。</summary>
        public void SubmitCommand(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));
            EnsureAttached();
            if (!_isConnected)
            {
                throw new InvalidOperationException("客户端未连接（未收到服务器快照），无法上行命令。");
            }

            _endpoint!.Send(LoopbackProtocol.WriteCommand(command).ToJson());
        }

        /// <summary>处理全部待收下行消息；返回处理条数。</summary>
        public int Pump()
        {
            EnsureAttached();
            int processed = 0;
            while (_endpoint!.TryReceive(out string raw))
            {
                ProcessOne(raw);
                processed++;
            }

            return processed;
        }

        private void ProcessOne(string raw)
        {
            JsonValue json = JsonValue.Parse(raw);

            if (LoopbackProtocol.TryReadSnapshot(json, out int snapVersion, out MatchState? snapState))
            {
                _view = snapState;
                _version = snapVersion;
                _isConnected = true;
                return;
            }

            if (LoopbackProtocol.TryReadStep(
                json, out int stepVersion, out bool accepted, out CommandError error, out bool finished, out List<StateChange>? changes))
            {
                if (!_isConnected || _view == null)
                {
                    throw new InvalidOperationException("未收到快照前收到 step 消息，协议顺序错误。");
                }

                _lastAccepted = accepted;
                _lastError = error;
                if (accepted && changes != null)
                {
                    StateChangeApplier.Apply(_view, changes);
                }

                _version = stepVersion;
                return;
            }

            throw new FormatException("未知下行消息 kind：" + raw);
        }

        private void EnsureAttached()
        {
            if (_endpoint == null)
            {
                throw new InvalidOperationException("客户端未 Attach 端点。");
            }
        }
    }
}
