using UnityEngine;

namespace Card.Presentation.Battle.Targeting
{
    /// <summary>
    /// 指向箭头表现（M5-T5，FR-8.3 / 03 U-8）：一条细矩形 <see cref="_line"/> 连接两端。
    /// 每次 <see cref="SetEndpoints"/> 都把两端屏幕坐标经
    /// <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle"/> 现算为
    /// <see cref="_area"/> 本地坐标——不缓存屏幕↔本地映射，因此分辨率变化后下一帧仍准确。
    /// 装配约定：<see cref="_line"/> 的父级须与 <see cref="_area"/> 同一坐标空间（通常为 Canvas 直下）。
    /// </summary>
    public sealed class TargetingArrowView : MonoBehaviour
    {
        [SerializeField] internal RectTransform _area = null!;
        [SerializeField] internal RectTransform _line = null!;
        [SerializeField] internal float _lineWidth = 8f;

        private Camera? _uiCamera;

        /// <summary>注入 UI 相机；Screen Space - Overlay 画布传 null。</summary>
        public void Initialize(Camera? uiCamera)
        {
            _uiCamera = uiCamera;
        }

        public void Show()
        {
            _line.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _line.gameObject.SetActive(false);
        }

        /// <summary>以屏幕坐标设置箭头两端；内部现算本地坐标并更新中点/角度/长度。</summary>
        public void SetEndpoints(Vector2 startScreen, Vector2 endScreen)
        {
            Vector2 start = ToLocal(startScreen);
            Vector2 end = ToLocal(endScreen);
            Vector2 delta = end - start;

            _line.anchoredPosition = (start + end) * 0.5f;
            _line.sizeDelta = new Vector2(delta.magnitude, _lineWidth);
            _line.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, _uiCamera, out Vector2 local);
            return local;
        }
    }
}
