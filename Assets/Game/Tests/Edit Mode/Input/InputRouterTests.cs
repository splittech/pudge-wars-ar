using Game.Core.Configs;
using Game.Core.Commands;
using NSubstitute;
using R3;
using Game.Input.Gestures;
using Game.Input.Routing;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Input
{
    public sealed class InputRouterTests
    {
        private const float GrabRadius = 1.5f;

        private Subject<Vector2> _onTap;
        private Subject<DragGesture> _onDrag;
        private IArenaSpace _arenaSpace;
        private ILocalHero _localHero;
        private IPlayerCommands _commands;
        private InputConfig _config;
        private InputRouter _router;

        [SetUp]
        public void SetUp()
        {
            _router = Setup.InputRouter(out _onTap, out _onDrag, out _arenaSpace, out _localHero, out _commands, out _config);
        }

        [TearDown]
        public void TearDown()
        {
            _router?.Dispose();
            _onTap?.Dispose();
            _onDrag?.Dispose();
            if (_config != null)
                Object.DestroyImmediate(_config);
        }

        [Test]
        public void Tap_PointIsOnArena_MoveCommandUsesArenaCoordinates()
        {
            // Arrange.
            Vector2 screenPoint = new(3f, 4f);

            // Act.
            _onTap.OnNext(screenPoint);

            // Assert.
            _commands.Received(1).MoveTo(new float2(3f, 4f));
            _commands.Received(1).MoveTo(Arg.Any<float2>());
        }

        [Test]
        public void Tap_PointIsOutsideArena_NoMoveCommandIsSent()
        {
            // Arrange.
            _arenaSpace.TryScreenToArena(Arg.Any<Vector2>(), out Arg.Any<float2>()).Returns(false);
            Vector2 screenPoint = new(3f, 4f);

            // Act.
            _onTap.OnNext(screenPoint);

            // Assert.
            _commands.DidNotReceive().MoveTo(Arg.Any<float2>());
        }

        [Test]
        public void Drag_StartedNearHeroAndReleasedOutsideGrabRadius_ThrowsInReleaseDirection()
        {
            // Arrange.
            Vector2 start = new(0.5f, 0f);

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, start, new Vector2(1f, 0f)));
            _onDrag.OnNext(new DragGesture(DragPhase.Moved, start, new Vector2(4f, 0f)));
            _onDrag.OnNext(new DragGesture(DragPhase.Ended, start, new Vector2(0f, 5f)));

            // Assert.
            _commands.Received(1).ThrowHook(new float2(0f, 1f));
            _commands.Received(1).ThrowHook(Arg.Any<float2>());
        }

        [Test]
        public void Drag_StartedFarFromHero_AimRemainsInactive()
        {
            // Arrange.
            Vector2 start = new(GrabRadius + 1f, 0f);

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, start, new Vector2(5f, 0f)));

            // Assert.
            Assert.That(_router.Aim.CurrentValue.IsActive, Is.False);
        }

        [Test]
        public void Drag_StartedFarFromHero_NoThrowCommandIsSent()
        {
            // Arrange.
            Vector2 start = new(GrabRadius + 1f, 0f);
            _onDrag.OnNext(new DragGesture(DragPhase.Began, start, new Vector2(5f, 0f)));

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Ended, start, new Vector2(8f, 0f)));

            // Assert.
            _commands.DidNotReceive().ThrowHook(Arg.Any<float2>());
        }

        [Test]
        public void Drag_FingerReturnsToHero_AimEntersCancelZone()
        {
            // Arrange.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, Vector2.zero, new Vector2(4f, 0f)));

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Moved, Vector2.zero, new Vector2(0.5f, 0f)));

            // Assert.
            Assert.That(_router.Aim.CurrentValue.IsInCancelZone, Is.True);
        }

        [Test]
        public void Drag_ReleasedInCancelZone_NoThrowCommandIsSent()
        {
            // Arrange.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, Vector2.zero, new Vector2(4f, 0f)));
            _onDrag.OnNext(new DragGesture(DragPhase.Moved, Vector2.zero, new Vector2(0.5f, 0f)));

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Ended, Vector2.zero, new Vector2(0.5f, 0f)));

            // Assert.
            _commands.DidNotReceive().ThrowHook(Arg.Any<float2>());
        }

        [Test]
        public void Drag_CancelledBeforeRelease_AimIsInactiveAndNoThrowIsSent()
        {
            // Arrange.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, Vector2.zero, new Vector2(4f, 0f)));

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Cancelled, Vector2.zero, new Vector2(4f, 0f)));
            _onDrag.OnNext(new DragGesture(DragPhase.Ended, Vector2.zero, new Vector2(4f, 0f)));

            // Assert.
            _commands.DidNotReceive().ThrowHook(Arg.Any<float2>());
            Assert.That(_router.Aim.CurrentValue.IsActive, Is.False);
        }

        [Test]
        public void Drag_HeroMovesDuringAim_OriginAndDirectionUseNewPosition()
        {
            // Arrange.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, Vector2.zero, new Vector2(4f, 0f)));
            _localHero.TryGetPosition(out Arg.Any<float2>()).Returns(call =>
            {
                call[0] = new float2(2f, 0f);
                return true;
            });

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Moved, Vector2.zero, new Vector2(2f, 5f)));

            // Assert.
            AimState aim = _router.Aim.CurrentValue;
            Assert.That(aim.IsActive, Is.True);
            Assert.That(aim.Origin, Is.EqualTo(new float2(2f, 0f)));
            Assert.That(aim.Direction, Is.EqualTo(new float2(0f, 1f)));
        }

        [Test]
        public void Drag_ReleasedAfterHeroMoved_AimIsReset()
        {
            // Arrange.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, Vector2.zero, new Vector2(4f, 0f)));
            _localHero.TryGetPosition(out Arg.Any<float2>()).Returns(call =>
            {
                call[0] = new float2(2f, 0f);
                return true;
            });
            _onDrag.OnNext(new DragGesture(DragPhase.Moved, Vector2.zero, new Vector2(2f, 5f)));

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Ended, Vector2.zero, new Vector2(2f, 5f)));

            // Assert.
            Assert.That(_router.Aim.CurrentValue.IsActive, Is.False);
        }

        [Test]
        public void Drag_HeroDiedWhileAiming_NoThrowCommandIsSent()
        {
            // Arrange.
            _onDrag.OnNext(new DragGesture(DragPhase.Began, Vector2.zero, new Vector2(4f, 0f)));
            _localHero.TryGetPosition(out Arg.Any<float2>()).Returns(false);

            // Act.
            _onDrag.OnNext(new DragGesture(DragPhase.Ended, Vector2.zero, new Vector2(5f, 0f)));

            // Assert.
            _commands.DidNotReceive().ThrowHook(Arg.Any<float2>());
        }
    }
}