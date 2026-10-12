using System;
using Card.Application.Launch;
using NUnit.Framework;

namespace Card.Tests.EditMode.Application
{
    /// <summary>
    /// NetLaunch 测试（M14-T4）：命令行参数 → 联机大厅自动动作的映射。
    /// 端口未指定（解析器 Port=0）必须落到调用方传入的默认端口（双开地址可复现）。
    /// </summary>
    [TestFixture]
    public class NetLaunchTests
    {
        private const int DefaultPort = 7777;

        [Test]
        public void NoArgs_None()
        {
            var action = NetLaunch.Resolve(DefaultPort, Array.Empty<string>());
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.None));
        }

        [Test]
        public void Host_NoPort_UsesDefaultPort()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "-host" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.Host));
            Assert.That(action.Port, Is.EqualTo(DefaultPort));
        }

        [Test]
        public void Host_WithPort_UsesSpecifiedPort()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "-host", "8080" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.Host));
            Assert.That(action.Port, Is.EqualTo(8080));
        }

        [Test]
        public void Host_EqualsSyntax_UsesSpecifiedPort()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "-host=9090" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.Host));
            Assert.That(action.Port, Is.EqualTo(9090));
        }

        [Test]
        public void Join_HostAndPort_BuildsAddress()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "-join", "127.0.0.1", "8080" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.Join));
            Assert.That(action.Port, Is.EqualTo(8080));
            Assert.That(action.Address, Is.EqualTo("127.0.0.1:8080"));
        }

        [Test]
        public void Join_HostOnly_UsesDefaultPort()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "-join", "127.0.0.1" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.Join));
            Assert.That(action.Port, Is.EqualTo(DefaultPort));
            Assert.That(action.Address, Is.EqualTo("127.0.0.1:" + DefaultPort));
        }

        [Test]
        public void Join_EqualsSyntax_BuildsAddress()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "-join=192.168.1.5:9090" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.Join));
            Assert.That(action.Port, Is.EqualTo(9090));
            Assert.That(action.Address, Is.EqualTo("192.168.1.5:9090"));
        }

        [Test]
        public void Join_MissingHost_Propagates()
        {
            Assert.Throws<ArgumentException>(() =>
                NetLaunch.Resolve(DefaultPort, new[] { "-join" }));
        }

        [Test]
        public void UnknownArg_None()
        {
            var action = NetLaunch.Resolve(DefaultPort, new[] { "--bogus" });
            Assert.That(action.Kind, Is.EqualTo(NetLaunchKind.None));
        }

        [Test]
        public void ToString_KindSpecific()
        {
            var none = NetLaunch.Resolve(DefaultPort, Array.Empty<string>());
            Assert.That(none.ToString(), Does.Contain("None"));

            var host = NetLaunch.Resolve(DefaultPort, new[] { "-host" });
            Assert.That(host.ToString(), Does.Contain("Host").And.Contain(DefaultPort.ToString()));

            var join = NetLaunch.Resolve(DefaultPort, new[] { "-join", "127.0.0.1" });
            Assert.That(join.ToString(), Does.Contain("Join").And.Contain("127.0.0.1"));
        }
    }
}
