using System.Collections.Generic;
using Game.Core.Configs;
using Game.Input.Gestures;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode.Input
{
    /// <summary>
    /// Feeds touches through an isolated Input System and checks the recognized gestures.
    /// Default InputConfig: tap up to 0.3 s, drag after 10 dp (a few pixels at editor DPI).
    /// </summary>
    public sealed class EnhancedTouchGesturesTests : InputTestFixture
    {
        private const double StartTime = 10.0;

        private Touchscreen _touchscreen;
        private InputConfig _config;
        private EnhancedTouchGestures _gestures;
        private CompositeDisposable _subscriptions;

        private readonly List<Vector2> _taps = new();
        private readonly List<DragPhase> _dragPhases = new();
        private readonly List<PinchTwistGesture> _pinchTwists = new();

        public override void Setup()
        {
            base.Setup();

            _touchscreen = InputSystem.AddDevice<Touchscreen>();
            _config = ScriptableObject.CreateInstance<InputConfig>();
            _gestures = new EnhancedTouchGestures(_config);

            _taps.Clear();
            _dragPhases.Clear();
            _pinchTwists.Clear();

            _subscriptions = new CompositeDisposable(
                _gestures.Tap.Subscribe(_taps.Add),
                _gestures.Drag.Subscribe(drag => _dragPhases.Add(drag.Phase)),
                _gestures.PinchTwist.Subscribe(_pinchTwists.Add));
        }

        public override void TearDown()
        {
            _subscriptions.Dispose();
            _gestures.Dispose();
            Object.DestroyImmediate(_config);

            base.TearDown();
        }

        [Test]
        public void Tick_TouchEndsWithinTapDuration_TapIsPublished()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            End(1, startPosition, StartTime + 0.1);

            // Assert.
            Assert.That(_taps, Is.EqualTo(new[] { new Vector2(100f, 100f) }));
            Assert.That(_dragPhases, Is.Empty);
        }

        [Test]
        public void Tick_TouchHasSmallJitter_TapIsPublishedWithoutDrag()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            Move(1, new Vector2(101f, 100f), StartTime + 0.05);
            End(1, new Vector2(101f, 100f), StartTime + 0.1);

            // Assert.
            Assert.That(_taps.Count, Is.EqualTo(1));
            Assert.That(_dragPhases, Is.Empty);
        }

        [Test]
        public void Tick_TouchExceedsTapDuration_NoTapIsPublished()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            End(1, startPosition, StartTime + 1.0);

            // Assert.
            Assert.That(_taps, Is.Empty);
        }

        [Test]
        public void Tick_TouchMovesBeyondThreshold_DragPhasesArePublishedInOrder()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            Move(1, new Vector2(150f, 100f), StartTime + 0.05);
            Move(1, new Vector2(200f, 100f), StartTime + 0.1);
            End(1, new Vector2(200f, 100f), StartTime + 0.15);

            // Assert.
            Assert.That(_dragPhases, Is.EqualTo(new[] { DragPhase.Began, DragPhase.Moved, DragPhase.Ended }));
            Assert.That(_taps, Is.Empty);
        }

        [Test]
        public void Tick_SecondFingerAppears_ActiveDragIsCancelled()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            Move(1, new Vector2(150f, 100f), StartTime + 0.05);
            Begin(2, new Vector2(300f, 300f), StartTime + 0.1);
            Move(1, new Vector2(200f, 100f), StartTime + 0.15);
            End(1, new Vector2(200f, 100f), StartTime + 0.2);

            // Assert.
            Assert.That(_dragPhases, Is.EqualTo(new[] { DragPhase.Began, DragPhase.Cancelled }));
            Assert.That(_taps, Is.Empty);
        }

        [Test]
        public void Tick_TwoFingersSpreadApart_PinchScaleIsPublished()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            Begin(2, new Vector2(200f, 100f), StartTime + 0.05);
            Move(2, new Vector2(300f, 100f), StartTime + 0.1);

            // Assert.
            Assert.That(_pinchTwists.Count, Is.EqualTo(1));
            Assert.That(_pinchTwists[0].ScaleFactor, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(_pinchTwists[0].AngleDelta, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Tick_TwoFingersRotate_TwistAngleIsPublished()
        {
            // Arrange.
            Vector2 startPosition = new(100f, 100f);

            // Act.
            Begin(1, startPosition, StartTime);
            Begin(2, new Vector2(200f, 100f), StartTime + 0.05);
            Move(2, new Vector2(100f, 200f), StartTime + 0.1);

            // Assert.
            Assert.That(_pinchTwists.Count, Is.EqualTo(1));
            Assert.That(_pinchTwists[0].AngleDelta, Is.EqualTo(90f).Within(1e-3f));
        }

        private void Begin(int touchId, Vector2 position, double time)
        {
            BeginTouch(touchId, position, screen: _touchscreen, time: time);
            _gestures.Tick();
        }

        private void Move(int touchId, Vector2 position, double time)
        {
            MoveTouch(touchId, position, screen: _touchscreen, time: time);
            _gestures.Tick();
        }

        private void End(int touchId, Vector2 position, double time)
        {
            EndTouch(touchId, position, screen: _touchscreen, time: time);
            _gestures.Tick();
        }
    }
}
