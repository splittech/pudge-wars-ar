using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class NetworkConnectionTests
    {
        [TestCase("127.0.0.1", "127.0.0.1")]
        [TestCase("192.168.1.10", "192.168.1.10")]
        [TestCase(" 10.0.0.1 ", "10.0.0.1")]
        [TestCase("169.254.1.2", "169.254.1.2")]
        public void TryValidate_UnicastAddress_ReturnsTrimmedAddress(string input, string expected)
        {
            // Act.
            bool valid = Ipv4Endpoint.TryValidate(input, out string address);

            // Assert.
            Assert.That(valid, Is.True);
            Assert.That(address, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("localhost")]
        [TestCase("::1")]
        [TestCase("127.1")]
        [TestCase("127.0.0.1:7777")]
        [TestCase("192.168.001.1")]
        [TestCase("192.168.1.256")]
        [TestCase("192.168.1.-1")]
        [TestCase("192.168.1. 1")]
        [TestCase("0.0.0.0")]
        [TestCase("224.0.0.1")]
        [TestCase("255.255.255.255")]
        [TestCase("192.168.1.1.2")]
        [TestCase("192.168.1.")]
        public void TryValidate_InvalidAddress_RejectsWithoutEndpoint(string input)
        {
            // Act.
            bool valid = Ipv4Endpoint.TryValidate(input, out string address);

            // Assert.
            Assert.That(valid, Is.False);
            Assert.That(address, Is.Empty);
        }

        [Test]
        public void Fail_ConnectedAttempt_IgnoresLateTimeout()
        {
            // Arrange.
            var state = new ConnectionAttemptState();
            long id = state.Begin(false);
            state.Complete(id);

            // Act.
            bool timedOut = state.Fail(id, "Timeout");

            // Assert.
            Assert.That(timedOut, Is.False);
            Assert.That(state.IsConnected, Is.True);
        }

        [Test]
        public void Fail_PreviousAttempt_DoesNotCancelNewConnection()
        {
            // Arrange.
            var state = new ConnectionAttemptState();
            long first = state.Begin(false);
            state.End(true);
            long second = state.Begin(false);

            // Act.
            bool failed = state.Fail(first, "Old timeout");

            // Assert.
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(failed, Is.False);
            Assert.That(state.IsPending, Is.True);
        }

        [Test]
        public void Complete_PreviousAttempt_DoesNotCompleteNewConnection()
        {
            // Arrange.
            var state = new ConnectionAttemptState();
            long first = state.Begin(false);
            state.End(true);
            state.Begin(false);

            // Act.
            bool completed = state.Complete(first);

            // Assert.
            Assert.That(completed, Is.False);
            Assert.That(state.IsPending, Is.True);
            Assert.That(state.IsConnected, Is.False);
        }

        [Test]
        public void End_AfterTimeout_PreservesFailureMessage()
        {
            // Arrange.
            var state = new ConnectionAttemptState();
            long id = state.Begin(false);
            state.Fail(id, "Connection timed out");

            // Act.
            state.End(false);
            state.End(true);

            // Assert.
            Assert.That(state.Message, Is.EqualTo("Connection timed out"));
            Assert.That(state.IsPending, Is.False);
        }

        [Test]
        public void End_HostDisconnects_StoresMessageForMenu()
        {
            // Arrange.
            var state = new ConnectionAttemptState();
            state.Complete(state.Begin(false));

            // Act.
            state.End(false);

            // Assert.
            Assert.That(state.IsConnected, Is.False);
            Assert.That(state.Message, Is.EqualTo("Connection closed by host."));
            Assert.That(state.HostDisconnected, Is.True);
            state.End(true);
            Assert.That(state.HostDisconnected, Is.True);
            state.AcknowledgeHostDisconnected();
            Assert.That(state.HostDisconnected, Is.False);
            Assert.That(state.Message, Is.Empty);
        }

        [Test]
        public void End_VoluntaryExit_DoesNotReportHostFailure()
        {
            // Arrange.
            var state = new ConnectionAttemptState();
            state.Complete(state.Begin(true));

            // Act.
            state.End(true);

            // Assert.
            Assert.That(state.Message, Is.EqualTo("Session ended."));
            Assert.That(state.HostDisconnected, Is.False);
        }

        [Test]
        public void End_AfterHandshakeRejection_PreservesReasonWithoutHostLostScreen()
        {
            var state = new ConnectionAttemptState();
            state.Fail(state.Begin(false), "Incompatible protocol");
            state.End(false);
            Assert.That(state.HostDisconnected, Is.False);
            Assert.That(state.Message, Is.EqualTo("Incompatible protocol"));
        }
    }
}
