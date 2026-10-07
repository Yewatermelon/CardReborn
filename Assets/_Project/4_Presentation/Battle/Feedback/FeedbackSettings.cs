using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 反馈设置（M5-T6；FR-8.2）：全局加速倍率 + 四类反馈独立开关。
    /// 可变引用共享：修改即时生效，播放器逐事件读取。表现层专用，不含规则语义。
    /// </summary>
    public sealed class FeedbackSettings
    {
        private const float MinSpeed = 0.01f;

        private float _speed = 1f;

        /// <summary>加速倍率：实际时长 = 基准时长 / Speed；setter 钳制为正数。</summary>
        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Max(MinSpeed, value);
        }

        /// <summary>浮动伤害/治疗/疲劳数字开关。</summary>
        public bool DamageNumbersEnabled { get; set; } = true;

        /// <summary>随从死亡淡出开关。</summary>
        public bool DeathFadeEnabled { get; set; } = true;

        /// <summary>回合/终局横幅开关。</summary>
        public bool TurnBannerEnabled { get; set; } = true;

        /// <summary>音效钩子总开关。</summary>
        public bool AudioEnabled { get; set; } = true;

        internal float ScaleDuration(float baseSeconds)
        {
            return baseSeconds / _speed;
        }
    }
}
