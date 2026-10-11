using Card.Bootstrap.Battle.Net;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// 联机大厅 overlay 测试（M14-T3 AC-14 + UI 工厂）：构建、标注文案、
    /// 阶段切换、按钮事件路由。
    /// </summary>
    [TestFixture]
    public class PvpLobbyUiTests
    {
        private GameObject _parent = null!;
        private PvpLobbyUi _ui = null!;
        private int _hostClicks;
        private int _joinClicks;
        private int _readyClicks;
        private int _backClicks;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("LobbyTestRoot");
            _ui = PvpLobbyUiFactory.Create(_parent.transform);
            _ui.HostClicked += () => _hostClicks++;
            _ui.JoinClicked += () => _joinClicks++;
            _ui.ReadyClicked += () => _readyClicks++;
            _ui.BackClicked += () => _backClicks++;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_parent);
        }

        [Test]
        public void Factory_CreatesUi_WithHostingDisclaimerSubtitle()
        {
            Assert.That(_ui.Root, Is.Not.Null);
            Assert.That(_ui.Root.activeSelf, Is.True);
            Assert.That(_ui.StatusText, Is.Not.Null);

            Transform subtitle = _ui.Root.transform.Find("Subtitle");
            Assert.That(subtitle, Is.Not.Null, "缺少副标签节点");
            TMP_Text text = subtitle.GetComponent<TextMeshProUGUI>();
            Assert.That(text.text, Is.EqualTo(PvpLobbyUi.SubtitleText));
        }

        [Test]
        public void SetPhase_TogglesRootAndGroups()
        {
            _ui.SetPhase(PvpLobbyPhase.Choose);
            Assert.That(_ui.Root.activeSelf, Is.True);

            _ui.SetPhase(PvpLobbyPhase.Hidden);
            Assert.That(_ui.Root.activeSelf, Is.False);

            _ui.SetPhase(PvpLobbyPhase.WaitingReady);
            Assert.That(_ui.Root.activeSelf, Is.True);
        }

        [Test]
        public void SetStatus_ErrorPrefix_TintsText()
        {
            _ui.SetStatus("正常状态");
            Color normal = _ui.StatusText.color;

            _ui.SetStatus("!出错了");
            Assert.That(_ui.StatusText.text, Is.EqualTo("!出错了"));
            Assert.That(_ui.StatusText.color, Is.Not.EqualTo(normal));
        }

        [Test]
        public void Address_RoundTripsThroughInputField()
        {
            _ui.AddressInput.text = "127.0.0.1:9999";
            Assert.That(_ui.Address, Is.EqualTo("127.0.0.1:9999"));

            _ui.AddressInput.text = "  ";
            Assert.That(_ui.Address, Is.EqualTo(PvpLobbyUi.DefaultAddress));
        }

        [Test]
        public void Buttons_RouteToEvents()
        {
            InvokeButton("PvpLobbyRoot/HostButton");
            InvokeButton("PvpLobbyRoot/JoinButton");
            InvokeButton("PvpLobbyRoot/ReadyGroup/ReadyButton");
            InvokeButton("PvpLobbyRoot/BackButton");

            Assert.That(_hostClicks, Is.EqualTo(1));
            Assert.That(_joinClicks, Is.EqualTo(1));
            Assert.That(_readyClicks, Is.EqualTo(1));
            Assert.That(_backClicks, Is.EqualTo(1));
        }

        private void InvokeButton(string path)
        {
            Transform found = _parent.transform.Find(path);
            Assert.That(found, Is.Not.Null, "找不到节点：" + path);
            Button button = found.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            button.onClick.Invoke();
        }
    }
}
