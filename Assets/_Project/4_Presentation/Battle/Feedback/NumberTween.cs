namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 整数数值补间（M14-T2，Docs/05 §5.5 数值 Tween）：生命/法力/攻血数字不做瞬时跳变。
    /// 线性插值 + 四舍五入取整；纯 C#，由视图在 Update 驱动 <see cref="Tick"/>（EditMode 测试直接驱动）。
    /// 首次 <see cref="To"/> 自动 Snap（不从 0 起播）；中途重定目标从当前显示值续播，无跳变。
    /// </summary>
    public sealed class NumberTween
    {
        /// <summary>视图缺省补间时长（秒）。</summary>
        internal const float DefaultDuration = 0.25f;

        private readonly float _duration;
        private int _from;
        private int _target;
        private float _elapsed;

        public NumberTween(float duration)
        {
            _duration = duration > 0f ? duration : 0.0001f;
        }

        /// <summary>当前显示值（补间期间为中间值）。</summary>
        public int Current { get; private set; }

        /// <summary>是否已被赋值（区分"从未显示"与"显示 0"）。</summary>
        public bool HasValue { get; private set; }

        public bool IsPlaying { get; private set; }

        /// <summary>立即到位并停止补间（复位契约与首次赋值路径）。</summary>
        public void Snap(int value)
        {
            Current = value;
            _target = value;
            HasValue = true;
            IsPlaying = false;
        }

        /// <summary>向目标补间；目标等于当前值时不播。</summary>
        public void To(int target)
        {
            if (!HasValue || target == Current)
            {
                Snap(target);
                return;
            }

            _from = Current;
            _target = target;
            _elapsed = 0f;
            IsPlaying = true;
        }

        /// <summary>推进补间；结束时 Current 恰为目标值并停止。</summary>
        public void Tick(float dt)
        {
            if (!IsPlaying)
            {
                return;
            }

            _elapsed += dt;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                Current = _target;
                IsPlaying = false;
                return;
            }

            Current = _from + (int)System.MathF.Round((_target - _from) * t);
        }
    }
}
