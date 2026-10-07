using UnityEngine;

namespace Card.Presentation.Battle.Targeting
{
    /// <summary>
    /// <see cref="IInputSource"/> 的生产实现（M5-T5）：旧输入管理器的薄适配。
    /// 确认=鼠标左键按下；取消=鼠标右键或 Esc 按下。边沿语义由 <c>GetMouseButtonDown</c>/<c>GetKeyDown</c> 保证。
    /// 注：<c>UnityEngine.Input</c> 必须全限定——同级命名空间 <c>Card.Presentation.Battle.Input</c> 会遮蔽它。
    /// </summary>
    public sealed class UnityInputSource : IInputSource
    {
        public Vector2 PointerScreenPosition
        {
            get { return UnityEngine.Input.mousePosition; }
        }

        public bool IsConfirmPressed
        {
            get { return UnityEngine.Input.GetMouseButtonDown(0); }
        }

        public bool IsCancelPressed
        {
            get { return UnityEngine.Input.GetMouseButtonDown(1) || UnityEngine.Input.GetKeyDown(KeyCode.Escape); }
        }
    }
}
