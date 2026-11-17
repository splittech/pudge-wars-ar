using Game.Core.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class PlacementReadinessTests
    {
        [Test]
        public void Confirm_RequiresEveryUniqueParticipant()
        {
            var readiness = new PlacementReadiness();
            readiness.Begin(new uint[] { 1, 2, 3 }, 10, 120);
            Assert.That(readiness.Confirm(1, 11), Is.True);
            Assert.That(readiness.Confirm(1, 12), Is.False);
            Assert.That(readiness.Confirm(99, 12), Is.False);
            Assert.That(readiness.Complete, Is.False);
            Assert.That(readiness.Confirm(2, 13), Is.True);
            Assert.That(readiness.Confirm(3, 14), Is.True);
            Assert.That(readiness.Complete, Is.True);
        }

        [Test]
        public void Deadline_RejectsLateConfirmation_AndKeepsPlayerForRemoval()
        {
            var readiness = new PlacementReadiness();
            readiness.Begin(new uint[] { 1, 2 }, 10, 120);
            readiness.Confirm(1, 11);
            Assert.That(readiness.Deadline, Is.EqualTo(130));
            Assert.That(readiness.Confirm(2, 130), Is.False);
            Assert.That(readiness.Pending, Is.EquivalentTo(new uint[] { 2 }));
            readiness.Remove(2);
            Assert.That(readiness.Complete, Is.True);
        }

        [Test]
        public void Begin_NewRound_DoesNotReusePreviousConfirmations()
        {
            var readiness = new PlacementReadiness();
            readiness.Begin(new uint[] { 1 }, 10, 120);
            readiness.Confirm(1, 11);
            readiness.Begin(new uint[] { 1, 2 }, 100, 120);
            Assert.That(readiness.Complete, Is.False);
            Assert.That(readiness.Pending, Is.EquivalentTo(new uint[] { 1, 2 }));
            Assert.That(readiness.Deadline, Is.EqualTo(220));
        }
    }
}
