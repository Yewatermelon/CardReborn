using System;
using NUnit.Framework;
using UnityEngine;
using Card.Presentation.Battle.Targeting;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>指向控制器测试共享夹具（M5-T5）：装配画布/箭头/控制器与三类 stub。</summary>
    internal abstract class TargetingTestFixture
    {
        protected GameObject Root = null!;
        protected TargetingArrowView Arrow = null!;
        protected TargetingController Controller = null!;
        protected StubInputSource Input = null!;
        protected StubTargetPicker Picker = null!;
        protected StubCommandSink Sink = null!;

        [SetUp]
        public void TargetingSetUp()
        {
            Root = new GameObject("Targeting_Test", typeof(RectTransform));
            RectTransform area = (RectTransform)Root.transform;
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(800f, 600f);
            area.position = Vector3.zero;

            var lineGo = new GameObject("Line", typeof(RectTransform));
            lineGo.transform.SetParent(area, false);
            var line = (RectTransform)lineGo.transform;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);

            Arrow = lineGo.AddComponent<TargetingArrowView>();
            Arrow._area = area;
            Arrow._line = line;
            Arrow._lineWidth = 4f;
            Arrow.Initialize(null);
            Arrow.Hide();

            Controller = Root.AddComponent<TargetingController>();
            Controller._localPlayerId = 3;
            Controller._arrow = Arrow;

            Input = new StubInputSource();
            Picker = new StubTargetPicker();
            Sink = new StubCommandSink();
        }

        [TearDown]
        public void TargetingTearDown()
        {
            UnityEngine.Object.DestroyImmediate(Root);
        }

        protected void Initialize()
        {
            Controller.Initialize(Input, Picker, Sink);
        }

        protected static Func<Vector2> FixedOrigin(Vector2 value)
        {
            return () => value;
        }
    }
}
