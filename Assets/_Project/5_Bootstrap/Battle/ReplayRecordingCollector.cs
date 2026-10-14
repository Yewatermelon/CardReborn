using System.Collections.Generic;
using Card.Application.Match;
using Card.Application.Replay;
using Card.Core;
using Card.Domain.Match;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 回放录制收集器（M15-T1）：<see cref="ICommandAuthority"/> 装饰器——插在
    /// <see cref="AgentMatchRunner"/> 与 <see cref="MatchController"/> 之间，
    /// 把所有<strong>被接受</strong>的命令（玩家 UI 与 AI 统一走 runner.Submit → 本类）
    /// 序列化为 JSON 追加进命令流水。构造时即持有 <see cref="MatchRecording"/>
    /// （Commands 引用内部 List，追加实时可见），终局时直接交给
    /// <see cref="ReplayFileRepository.Save"/> 写盘。
    /// 只录被接受命令：被拒命令零状态变更、不消耗随机数，重放结果与全录一致。
    /// 仅本地对局（PVE/热座）装配；联机命令流水在网络层，本期不录（M15-T1 遗留项）。
    /// </summary>
    internal sealed class ReplayRecordingCollector : ICommandAuthority
    {
        private readonly ICommandAuthority _inner;
        private readonly MatchRecording _recording;
        private readonly List<string> _commands = new List<string>();

        public ReplayRecordingCollector(
            ICommandAuthority inner,
            MatchSetupRequest seat0,
            MatchSetupRequest seat1,
            int seed)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
            _recording = new MatchRecording(
                Guard.NotNull(seat0, nameof(seat0)),
                Guard.NotNull(seat1, nameof(seat1)),
                seed,
                _commands);
        }

        /// <summary>录制中的对局数据（Commands 随 Submit 实时追加）。</summary>
        public MatchRecording Recording => _recording;

        public CommandResult Submit(IGameCommand command)
        {
            Guard.NotNull(command, nameof(command));
            CommandResult result = _inner.Submit(command);
            if (result.IsValid)
            {
                _commands.Add(CommandSerializer.Serialize(command));
            }

            return result;
        }
    }
}
