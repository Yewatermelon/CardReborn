using NUnit.Framework;
using TMPro;
using UnityEngine;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class ManaViewTests
    {
        private GameObject _root = null!;
        private ManaView _view = null!;
        private TMP_Text _currentText = null!;
        private TMP_Text _maxText = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ManaView_Test");
            _currentText = CreateText("Current");
            _maxText = CreateText("Max");
            _view = _root.AddComponent<ManaView>();
            _view._currentText = _currentText;
            _view._maxText = _maxText;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private TMP_Text CreateText(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            return go.AddComponent<TextMeshProUGUI>();
        }

        [Test]
        public void SetData_RendersCurrentAndMax()
        {
            _view.SetData(3, 5);

            Assert.That(_currentText.text, Is.EqualTo("3"));
            Assert.That(_maxText.text, Is.EqualTo("5"));
        }

        /// <summary>M14-T2（Docs/05 §5.5）：当前法力数值 Tween；上限立即刷新。</summary>
        [Test]
        public void SetData_CalledTwice_TweensCurrentThenOverwrites()
        {
            _view.SetData(3, 5);
            _view.SetData(1, 7);

            Assert.That(_currentText.text, Is.EqualTo("3"), "当前法力补间不得瞬时跳变");
            _view.Tick(1f);
            Assert.That(_currentText.text, Is.EqualTo("1"));
            Assert.That(_maxText.text, Is.EqualTo("7"), "上限不受补间影响，立即刷新");
        }

        [Test]
        public void SetData_ZeroMana_RendersZero()
        {
            _view.SetData(0, 10);

            Assert.That(_currentText.text, Is.EqualTo("0"));
            Assert.That(_maxText.text, Is.EqualTo("10"));
        }
    }
}
