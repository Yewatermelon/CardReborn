using NUnit.Framework;
using TMPro;
using UnityEngine;
using Card.Domain.Config;
using Card.Presentation.Battle;

namespace Card.Tests.EditMode.Presentation
{
    [TestFixture]
    public sealed class CardViewTests
    {
        private GameObject _root = null!;
        private CardView _view = null!;
        private TMP_Text _nameText = null!;
        private TMP_Text _descriptionText = null!;
        private TMP_Text _costText = null!;
        private TMP_Text _attackText = null!;
        private TMP_Text _healthText = null!;
        private GameObject _attackPanel = null!;
        private GameObject _healthPanel = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CardView_Test");
            _nameText = CreateText("Name");
            _descriptionText = CreateText("Desc");
            _costText = CreateText("Cost");
            _attackText = CreateText("Attack");
            _healthText = CreateText("Health");
            _attackPanel = new GameObject("AttackPanel");
            _healthPanel = new GameObject("HealthPanel");

            _view = _root.AddComponent<CardView>();
            _view._nameText = _nameText;
            _view._descriptionText = _descriptionText;
            _view._costText = _costText;
            _view._attackText = _attackText;
            _view._healthText = _healthText;
            _view._attackPanel = _attackPanel;
            _view._healthPanel = _healthPanel;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_attackPanel);
            Object.DestroyImmediate(_healthPanel);
        }

        private TMP_Text CreateText(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static CardViewData CreateMinionData()
        {
            return new CardViewData("Pango", "A penguin.", 2, 2, 3, "art_card_001", CardType.Minion);
        }

        private static CardViewData CreateSpellData()
        {
            return new CardViewData("Fireball", "Deal 6 damage.", 4, 0, 0, "art_spell_001", CardType.Spell);
        }

        [Test]
        public void SetData_Minion_RendersAllFields()
        {
            _view.SetData(CreateMinionData());

            Assert.That(_nameText.text, Is.EqualTo("Pango"));
            Assert.That(_descriptionText.text, Is.EqualTo("A penguin."));
            Assert.That(_costText.text, Is.EqualTo("2"));
            Assert.That(_attackText.text, Is.EqualTo("2"));
            Assert.That(_healthText.text, Is.EqualTo("3"));
            Assert.That(_attackPanel.activeSelf, Is.True);
            Assert.That(_healthPanel.activeSelf, Is.True);
        }

        [Test]
        public void SetData_Spell_HidesAttackAndHealthPanels()
        {
            _view.SetData(CreateSpellData());

            Assert.That(_nameText.text, Is.EqualTo("Fireball"));
            Assert.That(_costText.text, Is.EqualTo("4"));
            Assert.That(_attackPanel.activeSelf, Is.False);
            Assert.That(_healthPanel.activeSelf, Is.False);
        }

        [Test]
        public void SetData_Null_ThrowsArgumentNullException()
        {
            Assert.That(
                () => _view.SetData(null!),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("data"));
        }

        [Test]
        public void SetData_CalledTwice_OverwritesPreviousData()
        {
            _view.SetData(CreateMinionData());
            _view.SetData(CreateSpellData());

            Assert.That(_nameText.text, Is.EqualTo("Fireball"));
            Assert.That(_attackPanel.activeSelf, Is.False);
            Assert.That(_healthPanel.activeSelf, Is.False);
        }
    }
}
