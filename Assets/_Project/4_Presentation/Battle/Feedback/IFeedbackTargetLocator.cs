using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 反馈目标定位（M5-T6）：把事件载荷（随从 InstanceId / 英雄座位）翻译成世界坐标或视图。
    /// 生产实现（遍历双方 <see cref="Card.Presentation.Battle.BoardView"/> 子视图按
    /// <see cref="Card.Presentation.Battle.CardView.InstanceId"/> 匹配）属 M6 场景装配；
    /// 未命中返回 false，播放器跳过该条视觉反馈但不影响音效钩子。
    /// </summary>
    public interface IFeedbackTargetLocator
    {
        bool TryGetMinionAnchor(int instanceId, out Vector3 worldPosition);

        bool TryGetHeroAnchor(int seat, out Vector3 worldPosition);

        bool TryGetMinionView(int instanceId, out CardView? view);
    }
}
