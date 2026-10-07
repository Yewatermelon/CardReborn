using UnityEngine;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 场景相机（M6-T1）：两个引导场景均不手工摆相机；而无相机时渲染目标从不清屏，
    /// 移动 UI（指向箭头）会在上一帧画面上叠加形成拖影，Game 视图还常驻
    /// "No camera rendering" 提示。各引导器 Start 时无条件创建一枚场景级相机
    /// （随场景卸载，无需跨场景去重，也不使用任何 Find）。
    /// </summary>
    public static class SceneCamera
    {
        public static void Ensure()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
            camera.orthographic = true;
        }
    }
}
