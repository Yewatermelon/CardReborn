using Card.Domain.Match;
using UnityEngine;

namespace Card.Presentation.Battle.Targeting
{
    /// <summary>
    /// 命中测试抽象（M5-T5）：把"指针屏幕坐标下是什么实体"翻译成领域
    /// <see cref="TargetRef"/>（随从=InstanceId、英雄=座位 Id）。
    /// 只做命中识别，不做合法性过滤——目标合法性由权威侧 RuleEngine 裁定（铁律 5）。
    /// 生产实现（EventSystem/Physics 射线）随场景装配接入（M6-T1）。
    /// </summary>
    public interface ITargetPicker
    {
        /// <summary>命中返回 true 并输出目标；未命中返回 false 且 target 为 None。</summary>
        bool TryPickTarget(Vector2 screenPosition, out TargetRef target);
    }
}
