using System.Collections.Generic;
using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ReconnectRegistryTests
    {
        [Test]
        public void Expire_DeadlineReached_RemovesOnlyExpiredReservation()
        {
            // Arrange.
            var registry = new ReconnectRegistry();
            var removed = new List<uint>();
            registry.Reserve(1, 100);
            registry.Reserve(2, 110);

            // Act.
            registry.Expire(130, removed.Add);

            // Assert.
            Assert.That(removed, Is.EqualTo(new uint[] { 1 }));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Reserve_DuplicateDisconnect_DoesNotExtendDeadline()
        {
            // Arrange.
            var registry = new ReconnectRegistry();
            var removed = new List<uint>();
            registry.Reserve(1, 100);

            // Act.
            registry.Reserve(1, 120);
            registry.Expire(130, removed.Add);

            // Assert.
            Assert.That(removed, Is.EqualTo(new uint[] { 1 }));
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        public void Remove_BeforeDeadline_DoesNotExpireAgain()
        {
            // Arrange.
            var registry = new ReconnectRegistry();
            var removed = new List<uint>();
            registry.Reserve(1, 100);

            // Act.
            registry.Remove(1);
            registry.Expire(200, removed.Add);

            // Assert.
            Assert.That(removed, Is.Empty);
            Assert.That(registry.Count, Is.Zero);
        }
    }
}
