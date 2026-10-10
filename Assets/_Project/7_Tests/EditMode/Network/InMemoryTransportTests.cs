using System;
using System.Text;
using Card.Network;
using NUnit.Framework;

namespace Card.Tests.EditMode.Network
{
    /// <summary>
    /// InMemoryTransport 测试（M12-T2）：配对、Send/Receive、断开传播、顺序保证。
    /// </summary>
    [TestFixture]
    public class InMemoryTransportTests
    {
        [Test]
        public void CreatePair_TwoEndpointsExist()
        {
            InMemoryTransport.CreatePair(out var a, out var b);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(a.Connected, Is.True);
            Assert.That(b.Connected, Is.True);
        }

        [Test]
        public void Send_FromA_ReceiveAtB()
        {
            InMemoryTransport.CreatePair(out var a, out var b);
            byte[] payload = Encoding.UTF8.GetBytes("hello");
            a.Send(payload);

            Assert.That(b.TryReceive(out byte[]? received), Is.True);
            Assert.That(Encoding.UTF8.GetString(received!), Is.EqualTo("hello"));
        }

        [Test]
        public void Send_FromB_ReceiveAtA()
        {
            InMemoryTransport.CreatePair(out var a, out var b);
            byte[] payload = Encoding.UTF8.GetBytes("world");
            b.Send(payload);

            Assert.That(a.TryReceive(out byte[]? received), Is.True);
            Assert.That(Encoding.UTF8.GetString(received!), Is.EqualTo("world"));
        }

        [Test]
        public void TryReceive_NoData_ReturnsFalse()
        {
            InMemoryTransport.CreatePair(out var a, out _);
            Assert.That(a.TryReceive(out byte[]? _), Is.False);
        }

        [Test]
        public void Send_Multiple_ReceivedInOrder()
        {
            InMemoryTransport.CreatePair(out var a, out var b);
            a.Send(Encoding.UTF8.GetBytes("first"));
            a.Send(Encoding.UTF8.GetBytes("second"));
            a.Send(Encoding.UTF8.GetBytes("third"));

            Assert.That(b.TryReceive(out byte[]? r1), Is.True);
            Assert.That(Encoding.UTF8.GetString(r1!), Is.EqualTo("first"));
            Assert.That(b.TryReceive(out byte[]? r2), Is.True);
            Assert.That(Encoding.UTF8.GetString(r2!), Is.EqualTo("second"));
            Assert.That(b.TryReceive(out byte[]? r3), Is.True);
            Assert.That(Encoding.UTF8.GetString(r3!), Is.EqualTo("third"));
        }

        [Test]
        public void Dispose_A_TriggersB_OnDisconnected()
        {
            InMemoryTransport.CreatePair(out var a, out var b);
            bool disconnectedFired = false;
            b.OnDisconnected += () => disconnectedFired = true;

            a.Dispose();

            Assert.That(b.Connected, Is.False);
            Assert.That(disconnectedFired, Is.True);
        }

        [Test]
        public void Dispose_A_B_ConnectedBecomesFalse()
        {
            InMemoryTransport.CreatePair(out var a, out var b);
            a.Dispose();
            Assert.That(a.Connected, Is.False);
            Assert.That(b.Connected, Is.False);
        }

        [Test]
        public void Send_AfterDispose_ThrowsInvalidOperationException()
        {
            InMemoryTransport.CreatePair(out var a, out var _);
            a.Dispose();
            Assert.Throws<InvalidOperationException>(() => a.Send(Array.Empty<byte>()));
        }

        [Test]
        public void TryReceive_AfterDispose_ReturnsFalse()
        {
            InMemoryTransport.CreatePair(out var a, out var _);
            a.Dispose();
            Assert.That(a.TryReceive(out byte[]? _), Is.False);
        }
    }
}
