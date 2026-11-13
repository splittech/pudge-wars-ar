using Game.Core.Geometry;
using Game.Core.Hero;
using Game.Core.Match;
using Game.Net;
using Mirror;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MovementPredictionTests
    {
        [Test]
        public void Clock_SnapshotsEveryTick_DoNotStarveInputFrames()
        {
            // Arrange.
            var value = Predictor();
            var clock = new MovementClock();
            int produced = 0;
            // Act.
            for (uint tick = 0; tick < 30; tick++)
            {
                value.Receive(Snapshot(tick));
                for (int i = 0, count = clock.Advance(tick * (double)MovementPredictor.Delta); i < count; i++)
                    if (value.Step(out _)) produced++;
            }
            // Assert.
            Assert.That(produced, Is.EqualTo(30));
        }

        [Test]
        public void Clock_LongPause_BoundsCatchupAndRequestsResync()
        {
            // Arrange.
            var clock = new MovementClock();
            clock.Advance(0);
            // Act.
            int steps = clock.Advance(5);
            // Assert.
            Assert.That(steps, Is.EqualTo(4));
            Assert.That(clock.DroppedTime, Is.True);
            Assert.That(clock.Advance(5), Is.Zero);
        }

        private static MovementSnapshot Snapshot(uint tick = 0, uint ack = 0, float x = -4,
            bool alive = true, bool pulled = false, uint epoch = 1) => new()
        {
            ServerTick = tick, ServerTime = tick * (double)MovementPredictor.Delta,
            AckSequence = ack, SessionEpoch = epoch, Pos = new Vector2(x, 0),
            MoveTarget = new Vector2(x, 0), MoveSpeed = 3, Alive = alive, IsPulled = pulled
        };
        private static MovementPredictor Predictor()
        {
            var value = new MovementPredictor();
            value.Reset(1, new Rect2(new float2(-12, -7), new float2(-2, 7)));
            value.Receive(Snapshot());
            return value;
        }

        [Test]
        public void Step_NewTarget_MovesBeforeAcknowledgementAndKeepsMovingBetweenTaps()
        {
            // Arrange.
            var value = Predictor();
            value.Move(new Vector2(-2, 0));
            // Act.
            value.Step(out var first);
            value.Step(out _);
            // Assert.
            Assert.That(value.Position.x, Is.EqualTo(-3.8f).Within(0.00001));
            Assert.That(first.HasMoveTarget, Is.True);
            Assert.That(first.ClientTick, Is.EqualTo(1));
            Assert.That(first.SessionEpoch, Is.EqualTo(1));
        }

        [Test]
        public void Receive_SameTickState_ReplaysOnlyUnconfirmedTime()
        {
            // Arrange.
            var value = Predictor();
            value.Move(new Vector2(-2, 0));
            for (int i = 0; i < 3; i++) value.Step(out _);
            var snapshot = Snapshot(2, 2, -3.8f);
            snapshot.MoveTarget = new Vector2(-2, 0);
            // Act.
            value.Receive(snapshot);
            // Assert.
            Assert.That(value.Position.x, Is.EqualTo(-3.7f).Within(0.00001));
            Assert.That(value.Error, Is.LessThan(0.00001));
            Assert.That(value.HistorySize, Is.EqualTo(1));
            Assert.That(value.AckSequence, Is.EqualTo(2));
        }

        [Test]
        public void Receive_NewUnacknowledgedTarget_PreservesItAfterReplay()
        {
            // Arrange.
            var value = Predictor();
            value.Move(new Vector2(-2, 0));
            value.Step(out _);
            value.Move(new Vector2(-4, 2));
            value.Step(out _);
            var snapshot = Snapshot(1, 1, -3.9f);
            snapshot.MoveTarget = new Vector2(-2, 0);
            // Act.
            value.Receive(snapshot);
            value.Step(out var frame);
            // Assert.
            Assert.That(frame.HasMoveTarget, Is.True);
            Assert.That(frame.Target, Is.EqualTo(new Vector2(-4, 2)));
            Assert.That(value.Position.y, Is.GreaterThan(0));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Receive_ForcedState_ClearsMovementWithoutReplayingThroughIt(bool alive, bool pulled)
        {
            // Arrange.
            var value = Predictor();
            value.Move(new Vector2(-2, 0));
            for (int i = 0; i < 5; i++) value.Step(out _);
            // Act.
            value.Receive(Snapshot(3, 1, -5, alive, pulled));
            value.Step(out var frame);
            // Assert.
            Assert.That(value.Position.x, Is.EqualTo(-5));
            Assert.That(frame.HasMoveTarget, Is.False);
            Assert.That(value.Move(new Vector2(-2, 0)), Is.False);
        }

        [Test]
        public void Receive_HistoryOverflow_PerformsResyncInsteadOfInventingReplay()
        {
            // Arrange.
            var value = Predictor();
            for (int i = 0; i < 130; i++) value.Step(out _);
            // Act.
            value.Receive(Snapshot(1, 0, -6));
            // Assert.
            Assert.That(value.Resyncs, Is.EqualTo(1));
            Assert.That(value.HistorySize, Is.Zero);
            Assert.That(value.Position.x, Is.EqualTo(-6));
            Assert.That(value.Tick, Is.EqualTo(1));
        }

        [Test]
        public void Receive_StaleDuplicateOrWrongEpoch_DoesNotChangeState()
        {
            // Arrange.
            var value = Predictor();
            value.Receive(Snapshot(2));
            // Act and Assert.
            Assert.That(value.Receive(Snapshot(1, x: -7)), Is.False);
            Assert.That(value.Receive(Snapshot(2, x: -7)), Is.False);
            Assert.That(value.Receive(Snapshot(3, x: -7, epoch: 2)), Is.False);
            Assert.That(value.Position.x, Is.EqualTo(-4));
        }

        [Test]
        public void Step_HostMode_ProducesInputWithoutSecondMovementSimulation()
        {
            // Arrange.
            var value = Predictor();
            value.Move(new Vector2(-2, 0));
            // Act.
            value.Step(out var frame, false);
            // Assert.
            Assert.That(frame.HasMoveTarget, Is.True);
            Assert.That(value.Position.x, Is.EqualTo(-4));
        }

        [Test]
        public void Step_ReplayMatchesSharedMovementAndBounds()
        {
            // Arrange.
            var value = Predictor();
            var hero = Setup.Hero();
            var bounds = new Rect2(new float2(-12, -7), new float2(-2, 7));
            hero.MoveTarget = new float2(-2, 7);
            value.Move(new Vector2(100, 100));
            // Act.
            for (int i = 0; i < 120; i++)
            {
                value.Step(out _);
                HeroMovement.Step(ref hero, 3, bounds, MovementPredictor.Delta);
            }
            // Assert.
            Assert.That(value.Position, Is.EqualTo(new Vector2(hero.Pos.x, hero.Pos.y)));
        }

        [Test]
        public void Server_ManyFrames_UsesOneStepAndAcknowledgesLastAppliedSequence()
        {
            // Arrange.
            var simulation = Setup.MatchSimulation();
            var commands = new ServerPlayerCommands(simulation, Setup.Arena());
            for (uint i = 1; i <= 20; i++)
                Assert.That(commands.TryMovement(1, new MovementFrame
                {
                    Sequence = i, ClientTick = i, SessionEpoch = 1, HasMoveTarget = true, Target = new Vector2(-2.25f, 0)
                }, 1, 0, true, out _), Is.True);
            // Act.
            commands.Flush(0);
            simulation.Step(MovementPredictor.Delta);
            // Assert.
            Assert.That(math.distance(simulation.GetHero(0).Pos, new float2(-4, 0)),
                Is.EqualTo(Setup.HeroConfig().MoveSpeed * MovementPredictor.Delta).Within(0.00001));
            Assert.That(commands.MovementAck(1), Is.EqualTo(20));
        }

        [TestCase(0u, 1u, 1u, true)]
        [TestCase(1u, 999u, 1u, true)]
        [TestCase(1u, 1u, 2u, true)]
        [TestCase(1u, 1u, 1u, false)]
        public void Server_InvalidIdentityTickOrSequence_Rejects(uint sequence, uint tick, uint epoch, bool owner)
        {
            // Arrange.
            var commands = new ServerPlayerCommands(Setup.MatchSimulation(), Setup.Arena());
            // Act.
            bool result = commands.TryMovement(1, new MovementFrame
            {
                Sequence = sequence, ClientTick = tick, SessionEpoch = epoch
            }, 1, 0, owner, out _);
            // Assert.
            Assert.That(result, Is.False);
        }

        [TestCase(1)] [TestCase(4)]
        public void Batch_BoundedFrames_RoundTripsWithoutArrayAllocations(int count)
        {
            // Arrange.
            var batch = new MovementBatch();
            for (uint i = 1; i <= count; i++) batch.Add(new MovementFrame
                { Sequence = i, ClientTick = i, SessionEpoch = 1 });
            using var writer = NetworkWriterPool.Get();
            // Act.
            writer.WriteMovementBatch(batch);
            using var reader = NetworkReaderPool.Get(writer.ToArray());
            var actual = reader.ReadMovementBatch();
            // Assert.
            Assert.That(actual.Count, Is.EqualTo(count));
            Assert.That(actual.Get(count - 1).Sequence, Is.EqualTo(count));
            Assert.That(writer.Position, Is.EqualTo(1 + 13 * count));
            Assert.That(reader.Remaining, Is.Zero);
        }
    }
}
