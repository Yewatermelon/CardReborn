using NUnit.Framework;
using UnityEngine;
using Card.Presentation.Battle.Targeting;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class TargetingArrowViewTests
    {
        private GameObject _root = null!;
        private RectTransform _area = null!;
        private RectTransform _line = null!;
        private TargetingArrowView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Arrow_Test", typeof(RectTransform));
            _area = (RectTransform)_root.transform;
            _area.anchorMin = _area.anchorMax = new Vector2(0.5f, 0.5f);
            _area.pivot = new Vector2(0.5f, 0.5f);
            _area.sizeDelta = new Vector2(800f, 600f);
            _area.position = Vector3.zero;

            var lineGo = new GameObject("Line", typeof(RectTransform));
            lineGo.transform.SetParent(_area, false);
            _line = (RectTransform)lineGo.transform;
            _line.anchorMin = _line.anchorMax = new Vector2(0.5f, 0.5f);
            _line.pivot = new Vector2(0.5f, 0.5f);

            _view = lineGo.AddComponent<TargetingArrowView>();
            _view._area = _area;
            _view._line = _line;
            _view._lineWidth = 4f;
            _view.Initialize(null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Hide_DeactivatesLine_Show_Reactivates()
        {
            _view.Hide();
            Assert.That(_line.gameObject.activeSelf, Is.False);

            _view.Show();
            Assert.That(_line.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SetEndpoints_HorizontalLine_MidpointLengthAngleCorrect()
        {
            _view.SetEndpoints(new Vector2(0f, 0f), new Vector2(100f, 0f));

            Assert.That(_line.anchoredPosition.x, Is.EqualTo(50f).Within(0.01f));
            Assert.That(_line.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_line.sizeDelta.x, Is.EqualTo(100f).Within(0.01f));
            Assert.That(_line.sizeDelta.y, Is.EqualTo(4f).Within(0.01f));
            Assert.That(NormalizeAngle(_line.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void SetEndpoints_VerticalLine_AngleIs90()
        {
            _view.SetEndpoints(new Vector2(0f, 0f), new Vector2(0f, 60f));

            Assert.That(_line.sizeDelta.x, Is.EqualTo(60f).Within(0.01f));
            Assert.That(NormalizeAngle(_line.localEulerAngles.z), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void SetEndpoints_ScreenCenterMapsToAreaLocalZero()
        {
            // 模拟 800x600 overlay 画布：Unity 会把画布 RectTransform 移到 (400,300)
            _area.position = new Vector3(400f, 300f, 0f);

            _view.SetEndpoints(new Vector2(400f, 300f), new Vector2(400f, 300f));

            Assert.That(_line.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_line.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_line.sizeDelta.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void SetEndpoints_AfterResolutionChange_SameScreenPointMapsToNewLocal()
        {
            // 800x600：屏幕点 (400,300) → 本地 (0,0)
            _area.position = new Vector3(400f, 300f, 0f);
            _view.SetEndpoints(new Vector2(400f, 300f), new Vector2(500f, 300f));
            Assert.That(_line.anchoredPosition.x, Is.EqualTo(50f).Within(0.01f));
            Assert.That(_line.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));

            // 分辨率变为 1920x1080：同一屏幕点 (400,300) 应映射到本地 (-560,-240)
            _area.position = new Vector3(960f, 540f, 0f);
            _view.SetEndpoints(new Vector2(400f, 300f), new Vector2(500f, 300f));

            Assert.That(_line.anchoredPosition.x, Is.EqualTo(-510f).Within(0.01f));
            Assert.That(_line.anchoredPosition.y, Is.EqualTo(-240f).Within(0.01f));
        }

        [Test]
        public void SetEndpoints_ReversedDirection_AngleIs180()
        {
            _view.SetEndpoints(new Vector2(100f, 0f), new Vector2(0f, 0f));

            Assert.That(NormalizeAngle(_line.localEulerAngles.z), Is.EqualTo(180f).Within(0.01f));
        }

        private static float NormalizeAngle(float angle)
        {
            float a = angle % 360f;
            return a < 0f ? a + 360f : a;
        }
    }
}
