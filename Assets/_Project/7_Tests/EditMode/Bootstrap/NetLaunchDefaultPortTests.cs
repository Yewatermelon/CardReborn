using Card.Bootstrap.Battle;
using Card.Bootstrap.Battle.Net;
using NUnit.Framework;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// M14-T4：两处联机默认端口常量一致性（防漂移）。
    /// kernel 测试集不含 Bootstrap 源，故此断言只进 Unity EditMode 全量。
    /// </summary>
    [TestFixture]
    public class NetLaunchDefaultPortTests
    {
        [Test]
        public void PvpNetDefaultPort_MatchesLobbyDefault()
        {
            Assert.That(BattleSceneBootstrap.PvpNetDefaultPort,
                Is.EqualTo(PvpLobbyFlow.DefaultListenPort));
        }
    }
}
