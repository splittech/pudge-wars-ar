using Game.Core.Hook;
using Game.Net;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HookNetStateTests
    {
        [TestCase(9, 0)]
        [TestCase(10.25, 5)]
        [TestCase(100, 12)]
        public void EvaluateTip_Extending_ClampsElapsedDistance(double now, float distance)
        {
            // Arrange.
            var state = new HookNetState
            {
                Phase = HookPhase.Extending, Origin = new Vector2(-4, 2),
                Dir = Vector2.right, StartTime = 10
            };

            // Act.
            Vector2 tip = state.EvaluateTip(now, Vector2.zero, 20, 12);

            // Assert.
            Assert.That(tip, Is.EqualTo(new Vector2(-4 + distance, 2)));
        }

        [TestCase(HookPhase.Retracting)]
        [TestCase(HookPhase.Pulling)]
        public void EvaluateTip_AfterTurn_MovesFromTurnPointTowardsOwner(HookPhase phase)
        {
            // Arrange.
            var state = new HookNetState
            {
                Phase = phase, Origin = new Vector2(-4, 0), Dir = Vector2.right,
                TurnTime = 10, TurnLength = 8
            };

            // Act.
            Vector2 tip = state.EvaluateTip(10.1, new Vector2(-4, 0), 20, 12);

            // Assert.
            Assert.That(Vector2.Distance(tip, new Vector2(2, 0)), Is.LessThan(0.0001));
        }

        [Test]
        public void EvaluateTip_ReturnHasReachedOwner_DoesNotOvershoot()
        {
            // Arrange.
            var state = new HookNetState
            {
                Phase = HookPhase.Retracting, Dir = Vector2.right, TurnLength = 8, TurnTime = 10
            };
            Vector2 owner = new(-1, 3);

            // Act.
            Vector2 tip = state.EvaluateTip(100, owner, 20, 12);

            // Assert.
            Assert.That(tip, Is.EqualTo(owner));
        }

        [Test]
        public void EvaluateTip_Idle_ReturnsCurrentOwnerPosition()
        {
            // Arrange.
            var state = new HookNetState();
            Vector2 owner = new(-4, 1);

            // Act.
            Vector2 tip = state.EvaluateTip(100, owner, 20, 12);

            // Assert.
            Assert.That(tip, Is.EqualTo(owner));
        }

        [TestCase(0u)]
        [TestCase(42u)]
        [TestCase(uint.MaxValue)]
        public void Serialize_CompleteState_RoundTripsCompactPayload(uint targetNetId)
        {
            // Arrange.
            var expected = new HookNetState
            {
                Phase = HookPhase.Pulling, Origin = new Vector2(-4, 2), Dir = Vector2.right,
                StartTime = 100.125, TurnTime = 100.5, TurnLength = 7.5f, TargetNetId = targetNetId,
                RequestId = uint.MaxValue, SessionEpoch = 7
            };
            using var writer = NetworkWriterPool.Get();

            // Act.
            writer.WriteHookNetState(expected);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            HookNetState actual = reader.ReadHookNetState();

            // Assert.
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.Origin, Is.EqualTo(expected.Origin));
            Assert.That(actual.Dir, Is.EqualTo(expected.Dir));
            Assert.That(actual.StartTime, Is.EqualTo(expected.StartTime));
            Assert.That(actual.TurnTime, Is.EqualTo(expected.TurnTime));
            Assert.That(actual.TurnLength, Is.EqualTo(expected.TurnLength));
            Assert.That(actual.TargetNetId, Is.EqualTo(expected.TargetNetId));
            Assert.That(actual.RequestId, Is.EqualTo(expected.RequestId));
            Assert.That(actual.SessionEpoch, Is.EqualTo(expected.SessionEpoch));
            Assert.That(writer.Position, Is.EqualTo(49));
            Assert.That(reader.Remaining, Is.Zero);
        }
    }
}
