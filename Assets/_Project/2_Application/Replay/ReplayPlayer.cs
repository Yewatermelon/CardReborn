using System;
using Card.Application.Match;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;

namespace Card.Application.Replay
{
    /// <summary>
    /// 回放播放器状态机（M15-T1）：
    /// <c>Idle</c>（构造后）→ <c>Playing</c>（Play）→ <c>Paused</c>（Pause）→
    /// <c>Finished</c>（推进到末帧或 Seek 到终局）。
    /// </summary>
    public enum ReplayPlayerState
    {
        Idle,
        Playing,
        Paused,
        Finished
    }

    /// <summary>
    /// 交互式回放播放器（M15-T1 ★）：把 <see cref="MatchReplayer"/> 的"一口气跑完"
    /// 拆解为可单步推进的迭代器——内部维护一份 <see cref="MatchController"/>，
    /// 每次 <see cref="Step"/> 推进一帧命令；<see cref="Seek"/> 用"重建+快进到目标帧"
    /// 实现跳转（不做反向回退，简单可靠）。纯 BCL，节奏由表现层用协程/Update 控制。
    /// </summary>
    public sealed class ReplayPlayer
    {
        private readonly MatchRecording _recording;
        private readonly CardDatabase _database;
        private MatchState _state = null!;
        private MatchController _controller = null!;
        private int _currentFrame;
        private ReplayPlayerState _playerState = ReplayPlayerState.Idle;
        private float _speed = 1f;

        public ReplayPlayer(MatchRecording recording, CardDatabase database)
        {
            _recording = Guard.NotNull(recording, nameof(recording));
            _database = Guard.NotNull(database, nameof(database));
            Rebuild(0);
        }

        /// <summary>当前播放器状态。</summary>
        public ReplayPlayerState State => _playerState;

        /// <summary>已推进的帧数（命令序号）。</summary>
        public int CurrentFrame => _currentFrame;

        /// <summary>录制总帧数（命令条数）。</summary>
        public int TotalFrames => _recording.Commands.Count;

        /// <summary>当前帧的对局状态快照（引用语义，由表现层只读推送）。</summary>
        public MatchState CurrentSnapshot => _state;

        /// <summary>倍速（1/2/4 等正数）；只影响表现层推进节奏，不影响内核。</summary>
        public float Speed => _speed;

        /// <summary>开始播放；空录制直接转 Finished，否则转 Playing。</summary>
        public void Play()
        {
            if (_playerState == ReplayPlayerState.Playing)
            {
                return;
            }

            if (_playerState == ReplayPlayerState.Finished)
            {
                return;
            }

            if (TotalFrames == 0)
            {
                _playerState = ReplayPlayerState.Finished;
                return;
            }

            _playerState = ReplayPlayerState.Playing;
        }

        /// <summary>暂停；仅在 Playing 时生效。</summary>
        public void Pause()
        {
            if (_playerState == ReplayPlayerState.Playing)
            {
                _playerState = ReplayPlayerState.Paused;
            }
        }

        /// <summary>
        /// 前进一帧；到达末帧后返回 false（继续调用也安全）。
        /// 不检查当前状态——任何状态下都可推进（支持 Paused 单步、Idle 直接 Step）。
        /// </summary>
        public bool Step()
        {
            if (_currentFrame >= TotalFrames)
            {
                _playerState = ReplayPlayerState.Finished;
                return false;
            }

            IGameCommand command = CommandSerializer.Deserialize(_recording.Commands[_currentFrame]);
            _controller.Submit(command);
            _currentFrame++;

            if (_currentFrame >= TotalFrames)
            {
                _playerState = ReplayPlayerState.Finished;
            }

            return true;
        }

        /// <summary>
        /// 跳到 <paramref name="frame"/> 帧（[0, TotalFrames]）。
        /// 实现方式：从 seed 重建初始状态 + 提交到目标帧（不做反向回退）。
        /// 跳到末帧（含）转 Finished；否则转 Paused。
        /// </summary>
        public void Seek(int frame)
        {
            Guard.NotNegative(frame, nameof(frame));
            if (frame > TotalFrames)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frame), frame, "Seek 目标帧不能超过总帧数 " + TotalFrames + "。");
            }

            Rebuild(frame);
            _playerState = frame >= TotalFrames
                ? ReplayPlayerState.Finished
                : ReplayPlayerState.Paused;
        }

        /// <summary>设置倍速；非正数抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
        public void SetSpeed(float speed)
        {
            if (speed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "倍速必须为正数。");
            }

            _speed = speed;
        }

        /// <summary>
        /// 用 seed 重建初始状态并快进到 <paramref name="targetFrame"/> 帧。
        /// 终局后的命令 Submit 会被 RuleEngine 拒绝、状态零变更，
        /// 因此快进到 TotalFrames 也能得到正确终局状态。
        /// </summary>
        private void Rebuild(int targetFrame)
        {
            MatchState state = MatchFactory.Create(
                _database, _recording.Seat0, _recording.Seat1,
                new SeededRandomProvider(_recording.Seed));
            var controller = new MatchController(state, _database);

            for (int i = 0; i < targetFrame; i++)
            {
                IGameCommand cmd = CommandSerializer.Deserialize(_recording.Commands[i]);
                controller.Submit(cmd);
            }

            _state = state;
            _controller = controller;
            _currentFrame = targetFrame;
        }
    }
}
