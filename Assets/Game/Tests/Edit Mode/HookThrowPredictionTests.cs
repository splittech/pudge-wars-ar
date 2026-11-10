using Game.Core.Hook;
using Game.Net;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HookThrowPredictionTests
    {
        private HookThrowPrediction _prediction;

        [SetUp]
        public void SetUp()
        {
            _prediction = new HookThrowPrediction();
            _prediction.Reset(7);
        }

        [Test]
        public void TryBegin_BeforeServerResponse_ShowsNormalizedCosmeticHook()
        {
            // Act.
            bool began = _prediction.TryBegin(new Vector2(-4, 0), new Vector2(2, 0), 10, out uint id);
            bool visible = _prediction.TryGetState(10.05, default, out HookNetState state);

            // Assert.
            Assert.That(began, Is.True);
            Assert.That(visible, Is.True);
            Assert.That(id, Is.EqualTo(1));
            Assert.That(state.RequestId, Is.EqualTo(id));
            Assert.That(state.SessionEpoch, Is.EqualTo(7));
            Assert.That(state.Dir, Is.EqualTo(Vector2.right));
            Assert.That(state.Phase, Is.EqualTo(HookPhase.Extending));
            Assert.That(state.EvaluateTip(10.05, Vector2.zero, 20, 12).x, Is.EqualTo(-3).Within(0.0001));
        }

        [TestCase(HookPhase.Extending)]
        [TestCase(HookPhase.Pulling)]
        [TestCase(HookPhase.Idle)]
        public void TryGetState_MatchingSnapshot_ReplacesGhostInAnyPhase(HookPhase phase)
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint id);
            var authoritative = new HookNetState { RequestId = id, SessionEpoch = 7, Phase = phase };

            // Act.
            bool visible = _prediction.TryGetState(10.1, authoritative, out HookNetState state);

            // Assert.
            Assert.That(visible, Is.False);
            Assert.That(state.Phase, Is.EqualTo(phase));
            Assert.That(state.RequestId, Is.EqualTo(id));
        }

        [Test]
        public void ApplyResult_AcceptedBeforeSnapshot_KeepsGhostUntilSnapshot()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint id);

            // Act.
            _prediction.ApplyResult(id, 7, true);
            bool visible = _prediction.TryGetState(10.1, default, out _);

            // Assert.
            Assert.That(visible, Is.True);
        }

        [Test]
        public void ApplyResult_MatchingRejection_RemovesGhost()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint id);

            // Act.
            _prediction.ApplyResult(id, 7, false);

            // Assert.
            Assert.That(_prediction.IsPending, Is.False);
        }

        [Test]
        public void TryGetState_ResponseLost_ExpiresAtAbsoluteDeadline()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out _);

            // Act.
            bool visible = _prediction.TryGetState(10 + HookThrowPrediction.TimeoutSeconds, default, out _);

            // Assert.
            Assert.That(visible, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ApplyResult_OldRequestAfterTimeout_DoesNotCancelNewGhost(bool accepted)
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint oldId);
            _prediction.TryBegin(Vector2.zero, Vector2.up, 13, out uint newId);

            // Act.
            _prediction.ApplyResult(oldId, 7, accepted);
            bool visible = _prediction.TryGetState(13.1,
                new HookNetState { RequestId = oldId, SessionEpoch = 7, Phase = HookPhase.Extending }, out HookNetState state);

            // Assert.
            Assert.That(newId, Is.GreaterThan(oldId));
            Assert.That(visible, Is.True);
            Assert.That(state.RequestId, Is.EqualTo(newId));
            Assert.That(state.Dir, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void ApplyResult_SnapshotArrivedBeforeReply_DoesNotAffectNextRequest()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint oldId);
            var completed = new HookNetState { RequestId = oldId, SessionEpoch = 7, Phase = HookPhase.Idle };
            _prediction.TryGetState(10.1, completed, out _);
            _prediction.TryBegin(Vector2.zero, Vector2.up, 10.2, out uint newId);

            // Act.
            _prediction.ApplyResult(oldId, 7, false);
            bool visible = _prediction.TryGetState(10.3, completed, out HookNetState state);

            // Assert.
            Assert.That(visible, Is.True);
            Assert.That(state.RequestId, Is.EqualTo(newId));
        }

        [Test]
        public void Reset_NewEpoch_IgnoresOldEpochWithSameRequestId()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint oldId);

            // Act.
            _prediction.Reset(8);
            _prediction.TryBegin(Vector2.zero, Vector2.up, 10.1, out uint id);
            _prediction.ApplyResult(oldId, 7, false);
            bool visible = _prediction.TryGetState(10.2,
                new HookNetState { RequestId = oldId, SessionEpoch = 7 }, out HookNetState state);

            // Assert.
            Assert.That(id, Is.EqualTo(oldId));
            Assert.That(visible, Is.True);
            Assert.That(state.SessionEpoch, Is.EqualTo(8));
        }

        [Test]
        public void TryBegin_RequestStillPending_DoesNotOverwriteGhost()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint id);

            // Act.
            bool began = _prediction.TryBegin(Vector2.zero, Vector2.up, 10.1, out _);

            // Assert.
            Assert.That(began, Is.False);
            Assert.That(_prediction.RequestId, Is.EqualTo(id));
        }

        [Test]
        public void Cancel_ControlLost_RemovesGhostWithoutReusingRequestId()
        {
            // Arrange.
            _prediction.TryBegin(Vector2.zero, Vector2.right, 10, out uint first);

            // Act.
            _prediction.Cancel();
            _prediction.TryBegin(Vector2.zero, Vector2.up, 10.1, out uint second);

            // Assert.
            Assert.That(second, Is.GreaterThan(first));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryBegin_InvalidDirection_DoesNotCreateGhost(float value)
        {
            // Act.
            bool began = _prediction.TryBegin(Vector2.zero, new Vector2(value, 0), 10, out _);

            // Assert.
            Assert.That(began, Is.False);
            Assert.That(_prediction.IsPending, Is.False);
        }

        [Test]
        public void TryConsume_RepeatedOrOutOfOrderRequest_RejectsReplay()
        {
            // Arrange.
            var sequence = new ActionRequestSequence();
            sequence.Reset(7);
            sequence.TryConsume(3, 7, out _);

            // Act and Assert.
            Assert.That(sequence.TryConsume(3, 7, out _), Is.False);
            Assert.That(sequence.TryConsume(2, 7, out _), Is.False);
            Assert.That(sequence.TryConsume(0, 7, out _), Is.False);
            Assert.That(sequence.TryConsume(4, 6, out _), Is.False);
            Assert.That(sequence.TryConsume(4, 7, out _), Is.True);
        }

        [Test]
        public void TryConsume_NewEpoch_AllowsFreshSequence()
        {
            // Arrange.
            var sequence = new ActionRequestSequence();
            sequence.Reset(7);
            sequence.TryConsume(uint.MaxValue, 7, out _);

            // Act.
            sequence.Reset(8);
            bool accepted = sequence.TryConsume(1, 8, out _);

            // Assert.
            Assert.That(accepted, Is.True);
            Assert.That(sequence.TryConsume(2, 7, out _), Is.False);
        }
    }
}
