using UnityEngine;

namespace Card.Presentation.Battle.Targeting
{
    /// <summary>
    /// 输入抽象（M5-T5，03 U-9）：指向/确认/取消三类输入的轮询视图。
    /// "Pressed" 为本帧边沿语义（按下那一帧为 true），由实现方保证。
    /// 业务代码不直接读 <c>Input.*</c>，便于换新输入系统/触屏。
    /// </summary>
    public interface IInputSource
    {
        /// <summary>指针当前屏幕坐标（像素）。</summary>
        Vector2 PointerScreenPosition { get; }

        /// <summary>本帧确认（左键按下）。</summary>
        bool IsConfirmPressed { get; }

        /// <summary>本帧取消（右键或 Esc 按下）。</summary>
        bool IsCancelPressed { get; }
    }
}
