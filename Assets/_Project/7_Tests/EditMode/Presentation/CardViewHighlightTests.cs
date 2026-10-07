using Card.Domain.Config;
using Card.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>卡牌高亮与 InstanceId 同步（M5-T6；FR-8.6）：三态面板按旗标显隐。</summary>
    [TestFixture]
    internal sealed class CardViewHighlightTests
    {
        private CardView _view = null!;
        private CardView _prefab = null!;

        [SetUp]
        public void SetUp()
        {
            _prefab = PresentationTestPrefabs.CreateCardViewPrefab();
            _view = Object.Instantiate(_prefab);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view.gameObject);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void SetHighlight_None_HidesAllPanels()
        {
            _view.SetHighlight(CardHighlight.Playable | CardHighlight.Attackable | CardHighlight.Taunt);

            _view.SetHighlight(CardHighlight.None);

            Assert.IsFalse(_view._playableHighlight.activeSelf);
            Assert.IsFalse(_view._attackableHighlight.activeSelf);
            Assert.IsFalse(_view._tauntHighlight.activeSelf);
        }

        [Test]
        public void SetHighlight_Playable_ShowsOnlyPlayablePanel()
        {
            _view.SetHighlight(CardHighlight.Playable);

            Assert.IsTrue(_view._playableHighlight.activeSelf);
            Assert.IsFalse(_view._attackableHighlight.activeSelf);
            Assert.IsFalse(_view._tauntHighlight.activeSelf);
        }

        [Test]
        public void SetHighlight_AttackableAndTaunt_ShowsBothPanels()
        {
            _view.SetHighlight(CardHighlight.Attackable | CardHighlight.Taunt);

            Assert.IsFalse(_view._playableHighlight.activeSelf);
            Assert.IsTrue(_view._attackableHighlight.activeSelf);
            Assert.IsTrue(_view._tauntHighlight.activeSelf);
        }

        [Test]
        public void SetHighlight_Taunt_ShowsOnlyTauntPanel()
        {
            _view.SetHighlight(CardHighlight.Taunt);

            Assert.IsFalse(_view._playableHighlight.activeSelf);
            Assert.IsFalse(_view._attackableHighlight.activeSelf);
            Assert.IsTrue(_view._tauntHighlight.activeSelf);
        }

        [Test]
        public void SetData_SyncsInstanceIdIncludingNull()
        {
            _view.SetData(new CardViewData("n", "d", 2, 1, 1, "art", CardType.Minion, 42));
            Assert.AreEqual(42, _view.InstanceId);

            _view.SetData(new CardViewData("n", "d", 2, 1, 1, "art", CardType.Minion));
            Assert.IsNull(_view.InstanceId);
        }
    }
}
