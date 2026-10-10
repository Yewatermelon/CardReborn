using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Bootstrap
{
    /// <summary>
    /// CommandLineRoleParser 测试（M12-T8）：参数组合全覆盖。
    /// </summary>
    [TestFixture]
    public class CommandLineRoleParserTests
    {
        [Test]
        public void NoArgs_DefaultsToHotSeat()
        {
            var result = CommandLineRoleParser.Parse(Array.Empty<string>());
            Assert.That(result.Role, Is.EqualTo(NetworkRole.HotSeat));
            Assert.That(result.Port, Is.EqualTo(0));
        }

        [Test]
        public void Host_NoPort_OsAssigns()
        {
            var result = CommandLineRoleParser.Parse(new[] { "-host" });
            Assert.That(result.Role, Is.EqualTo(NetworkRole.PeerHost));
            Assert.That(result.Port, Is.EqualTo(0));
        }

        [Test]
        public void Host_WithPort_Specified()
        {
            var result = CommandLineRoleParser.Parse(new[] { "-host", "8080" });
            Assert.That(result.Role, Is.EqualTo(NetworkRole.PeerHost));
            Assert.That(result.Port, Is.EqualTo(8080));
        }

        [Test]
        public void Host_WithEqualsSyntax()
        {
            var result = CommandLineRoleParser.Parse(new[] { "-host=9090" });
            Assert.That(result.Role, Is.EqualTo(NetworkRole.PeerHost));
            Assert.That(result.Port, Is.EqualTo(9090));
        }

        [Test]
        public void Join_HostAndPort()
        {
            var result = CommandLineRoleParser.Parse(new[] { "-join", "127.0.0.1", "8080" });
            Assert.That(result.Role, Is.EqualTo(NetworkRole.JoinClient));
            Assert.That(result.HostAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(result.Port, Is.EqualTo(8080));
        }

        [Test]
        public void Join_HostOnly_PortDefaultZero()
        {
            var result = CommandLineRoleParser.Parse(new[] { "-join", "127.0.0.1" });
            Assert.That(result.Role, Is.EqualTo(NetworkRole.JoinClient));
            Assert.That(result.HostAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(result.Port, Is.EqualTo(0));
        }

        [Test]
        public void Join_NoHost_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                CommandLineRoleParser.Parse(new[] { "-join" }));
        }

        [Test]
        public void UnknownArg_FallsBackToHotSeat()
        {
            var result = CommandLineRoleParser.Parse(new[] { "--bogus" });
            Assert.That(result.Role, Is.EqualTo(NetworkRole.HotSeat));
        }

        [Test]
        public void NullArgs_UsesEnvironment()
        {
            // 只验证 null 不抛异常——实际行为取决于进程命令行
            Assert.DoesNotThrow(() => CommandLineRoleParser.Parse(null));
        }

        [Test]
        public void ParseResult_ToString_RoleSpecific()
        {
            var host = CommandLineRoleParser.Parse(new[] { "-host", "8080" });
            Assert.That(host.ToString(), Does.Contain("PeerHost").And.Contain("8080"));

            var join = CommandLineRoleParser.Parse(new[] { "-join", "127.0.0.1", "8080" });
            Assert.That(join.ToString(), Does.Contain("JoinClient").And.Contain("127.0.0.1"));

            var hot = CommandLineRoleParser.Parse(Array.Empty<string>());
            Assert.That(hot.ToString(), Does.Contain("HotSeat"));
        }
    }
}
