using NUnit.Framework;
using TMPro;
using UnityEngine;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class HeroViewTests
    {
        private GameObject _root = null!;
        private HeroView _view = null!;
        private TMP_Text _healthText = null!;
        private TMP_Text _armorText = null!;
        private TMP_Text _nameText = null!;
        private GameObject _armorPanel = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HeroView_Test");
            _healthText = CreateText("Health");
            _armorText = CreateText("Armor");
            _nameText = CreateText("Name");
            _armorPanel = new GameObject("ArmorPanel");
            _armorPanel.transform.SetParent(_root.transform);

            _view = _root.AddComponent<HeroView>();
            _view._healthText = _healthText;
            _view._armorText = _armorText;
            _view._armorPanel = _armorPanel;
            _view._nameText = _nameText;
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
        public void SetData_RendersHealth()
        {
            _view.SetData(25, 30, 0);

            Assert.That(_healthText.text, Is.EqualTo("25"));
        }

        [Test]
        public void SetData_WithArmor_ShowsArmorPanel()
        {
            _view.SetData(25, 30, 4);

            Assert.That(_armorPanel.activeSelf, Is.True);
            Assert.That(_armorText.text, Is.EqualTo("4"));
        }

        [Test]
        public void SetData_ZeroArmor_HidesArmorPanel()
        {
            _view.SetData(30, 30, 2);
            _view.SetData(30, 30, 0);

            Assert.That(_armorPanel.activeSelf, Is.False);
        }

        [Test]
        public void SetData_CalledTwice_OverwritesPreviousData()
        {
            _view.SetData(25, 30, 4);
            _view.SetData(12, 30, 1);

            Assert.That(_healthText.text, Is.EqualTo("12"));
            Assert.That(_armorText.text, Is.EqualTo("1"));
            Assert.That(_armorPanel.activeSelf, Is.True);
        }

        [Test]
        public void SetName_RendersNameKey()
        {
            _view.SetName("HERO_001_NAME");

            Assert.That(_nameText.text, Is.EqualTo("HERO_001_NAME"));
        }
    }
}
