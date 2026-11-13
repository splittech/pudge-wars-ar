using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class LanServerListTests
    {
        [Test]
        public void Observe_SameServerOnTwoInterfaces_UpdatesSingleEntryAndExpiry()
        {
            var list = new LanServerList();
            list.Observe(1, "192.168.1.2", 0);
            list.Observe(1, "10.0.0.2", 5);
            list.RemoveExpired(10);
            Assert.That(list.Servers.Count, Is.EqualTo(1));
            Assert.That(list.Servers[0].Address, Is.EqualTo("10.0.0.2"));
            list.RemoveExpired(15);
            Assert.That(list.Servers, Is.Empty);
        }

        [Test]
        public void RemoveExpired_KeepsOnlyRecentlySeenServers()
        {
            var list = new LanServerList();
            list.Observe(1, "192.168.1.2", 1);
            list.Observe(2, "192.168.1.3", 7);
            list.RemoveExpired(11);
            Assert.That(list.Servers.Count, Is.EqualTo(1));
            Assert.That(list.Servers[0].Id, Is.EqualTo(2));
        }

        [TestCase("255.255.255.255", 0)]
        [TestCase("127.0.0.1", double.NaN)]
        [TestCase("127.0.0.1", double.PositiveInfinity)]
        [TestCase("::1", 0)]
        public void Observe_InvalidEndpointOrTime_DoesNotCreateEntry(string address, double time)
        {
            var list = new LanServerList();
            list.Observe(1, address, time);
            Assert.That(list.Servers, Is.Empty);
        }

        [Test]
        public void Observe_AtCapacity_StillRefreshesExistingServer()
        {
            var list = new LanServerList();
            for (int i = 0; i <= LanServerList.Capacity; i++)
                list.Observe(i, "127.0.0.1", 0);
            Assert.That(list.Servers.Count, Is.EqualTo(LanServerList.Capacity));
            list.Observe(0, "127.0.0.2", 5);
            list.RemoveExpired(10);
            Assert.That(list.Servers.Count, Is.EqualTo(1));
            Assert.That(list.Servers[0].Address, Is.EqualTo("127.0.0.2"));
            list.Clear();
            Assert.That(list.Servers, Is.Empty);
        }
    }
}
