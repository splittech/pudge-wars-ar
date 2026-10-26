using System.Collections.Generic;
using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Economy;
using Game.Input.Gestures;
using Game.Input.Routing;
using NUnit.Framework;
using R3;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Input
{
    public sealed class InputRouterTests
    {
        // Default InputConfig grab radius (FR-IN-02).
        private const float GrabRadius = 1.5f;

        private FakeGestures _gestures;
        private FakeArenaSpace _arenaSpace;
        private FakeLocalHero _localHero;
        private FakePlayerCommands _commands;
        private InputConfig _config;
        private InputRouter _router;

        [SetUp]
        public void SetUp()
        {
            _gestures = new FakeGestures();
            _arenaSpace = new FakeArenaSpace();
            _localHero = new FakeLocalHero { Position = new float2(0f, 0f) };
            _commands = new FakePlayerCommands();
            _config = ScriptableObject.CreateInstance<InputConfig>();

            _router = new InputRouter(_gestures, _arenaSpace, _localHero, _commands, _config);
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
            _gestures.Dispose();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Tap_OnArena_MovesToArenaPoint()
        {
            _gestures.Tap(new Vector2(3f, 4f));

            Assert.That(_commands.MoveTargets, Is.EqualTo(new[] { new float2(3f, 4f) }));
        }

        [Test]
        public void Tap_OffArena_IsIgnored()
        {
            _arenaSpace.IsOnArena = false;

            _gestures.Tap(new Vector2(3f, 4f));

            Assert.That(_commands.MoveTargets, Is.Empty);
        }

        [Test]
        public void Drag_FromHeroReleasedOutsideGrabRadius_ThrowsInDragDirection()
        {
            _gestures.Drag(DragPhase.Began, new Vector2(0.5f, 0f), new Vector2(1f, 0f));
            _gestures.Drag(DragPhase.Moved, new Vector2(0.5f, 0f), new Vector2(4f, 0f));
            _gestures.Drag(DragPhase.Ended, new Vector2(0.5f, 0f), new Vector2(0f, 5f));

            Assert.That(_commands.ThrowDirections, Is.EqualTo(new[] { new float2(0f, 1f) }));
        }

        [Test]
        public void Drag_StartedFarFromHero_DoesNotAimOrThrow()
        {
            _gestures.Drag(DragPhase.Began, new Vector2(GrabRadius + 1f, 0f), new Vector2(5f, 0f));

            Assert.That(_router.Aim.CurrentValue.IsActive, Is.False);

            _gestures.Drag(DragPhase.Ended, new Vector2(GrabRadius + 1f, 0f), new Vector2(8f, 0f));

            Assert.That(_commands.ThrowDirections, Is.Empty);
        }

        [Test]
        public void Drag_ReturnedToHero_CancelsThrow()
        {
            _gestures.Drag(DragPhase.Began, new Vector2(0f, 0f), new Vector2(4f, 0f));
            _gestures.Drag(DragPhase.Moved, new Vector2(0f, 0f), new Vector2(0.5f, 0f));

            Assert.That(_router.Aim.CurrentValue.IsInCancelZone, Is.True);

            _gestures.Drag(DragPhase.Ended, new Vector2(0f, 0f), new Vector2(0.5f, 0f));

            Assert.That(_commands.ThrowDirections, Is.Empty);
        }

        [Test]
        public void Drag_Cancelled_DoesNotThrow()
        {
            _gestures.Drag(DragPhase.Began, new Vector2(0f, 0f), new Vector2(4f, 0f));
            _gestures.Drag(DragPhase.Cancelled, new Vector2(0f, 0f), new Vector2(4f, 0f));
            _gestures.Drag(DragPhase.Ended, new Vector2(0f, 0f), new Vector2(4f, 0f));

            Assert.That(_commands.ThrowDirections, Is.Empty);
            Assert.That(_router.Aim.CurrentValue.IsActive, Is.False);
        }

        [Test]
        public void Aim_FollowsMovingHeroAndResetsAfterRelease()
        {
            _gestures.Drag(DragPhase.Began, new Vector2(0f, 0f), new Vector2(4f, 0f));
            _localHero.Position = new float2(2f, 0f);
            _gestures.Drag(DragPhase.Moved, new Vector2(0f, 0f), new Vector2(2f, 5f));

            AimState aim = _router.Aim.CurrentValue;
            Assert.That(aim.IsActive, Is.True);
            Assert.That(aim.Origin, Is.EqualTo(new float2(2f, 0f)));
            Assert.That(aim.Direction, Is.EqualTo(new float2(0f, 1f)));

            _gestures.Drag(DragPhase.Ended, new Vector2(0f, 0f), new Vector2(2f, 5f));

            Assert.That(_router.Aim.CurrentValue.IsActive, Is.False);
        }

        [Test]
        public void Drag_HeroDiedWhileAiming_DoesNotThrow()
        {
            _gestures.Drag(DragPhase.Began, new Vector2(0f, 0f), new Vector2(4f, 0f));
            _localHero.IsAlive = false;
            _gestures.Drag(DragPhase.Ended, new Vector2(0f, 0f), new Vector2(5f, 0f));

            Assert.That(_commands.ThrowDirections, Is.Empty);
        }

        private sealed class FakeGestures : ITouchGestures
        {
            private readonly Subject<Vector2> _tap = new();
            private readonly Subject<DragGesture> _drag = new();
            private readonly Subject<PinchTwistGesture> _pinchTwist = new();

            Observable<Vector2> ITouchGestures.Tap => _tap;
            Observable<DragGesture> ITouchGestures.Drag => _drag;
            Observable<PinchTwistGesture> ITouchGestures.PinchTwist => _pinchTwist;

            public void Tap(Vector2 position)
            {
                _tap.OnNext(position);
            }

            public void Drag(DragPhase phase, Vector2 start, Vector2 position)
            {
                _drag.OnNext(new DragGesture(phase, start, position));
            }

            public void Dispose()
            {
                _tap.Dispose();
                _drag.Dispose();
                _pinchTwist.Dispose();
            }
        }

        /// <summary>
        /// Maps screen points one-to-one onto the arena.
        /// </summary>
        private sealed class FakeArenaSpace : IArenaSpace
        {
            public bool IsOnArena = true;

            public bool TryScreenToArena(Vector2 screenPoint, out float2 arenaPoint)
            {
                arenaPoint = new float2(screenPoint.x, screenPoint.y);
                return IsOnArena;
            }

            public Vector3 ArenaToWorld(float2 arenaPoint)
            {
                return new Vector3(arenaPoint.x, 0f, arenaPoint.y);
            }
        }

        private sealed class FakeLocalHero : ILocalHero
        {
            public float2 Position;
            public bool IsAlive = true;

            public bool TryGetPosition(out float2 position)
            {
                position = Position;
                return IsAlive;
            }
        }

        private sealed class FakePlayerCommands : IPlayerCommands
        {
            public readonly List<float2> MoveTargets = new();
            public readonly List<float2> ThrowDirections = new();

            public void MoveTo(float2 arenaPoint)
            {
                MoveTargets.Add(arenaPoint);
            }

            public void ThrowHook(float2 direction)
            {
                ThrowDirections.Add(direction);
            }

            public void BuyUpgrade(UpgradeId id) { }
        }
    }
}
