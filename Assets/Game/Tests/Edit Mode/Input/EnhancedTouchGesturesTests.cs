using System.Collections.Generic;
using System.Reflection;
using Game.Core.Configs;
using Game.Input.Gestures;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private GameObject _uiRoot;
        private EventSystem _previousEventSystem;

        private readonly List<Vector2> _taps = new();
        private readonly List<DragPhase> _dragPhases = new();
        private readonly List<PinchTwistGesture> _pinchTwists = new();

        public override void Setup()
        {
            base.Setup();
            _previousEventSystem = EventSystem.current;

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
            if (_uiRoot != null)
            {
                SetUiLifecycle("OnDisable");
                Object.DestroyImmediate(_uiRoot);
            }

            if (_previousEventSystem != null)
            {
                EventSystem.current = _previousEventSystem;
            }
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

        [TestCase(1)]
        [TestCase(2)]
        public void Tick_EitherFingerStartsOverUi_PinchRemainsBlockedAfterLeavingUi(int blockedFinger)
        {
            CreateUi();
            Vector2 first = blockedFinger == 1 ? new Vector2(20f, 100f) : new Vector2(100f, 100f);
            Vector2 second = blockedFinger == 2 ? new Vector2(20f, 100f) : new Vector2(200f, 100f);

            Begin(1, first, StartTime);
            Begin(2, second, StartTime + 0.05);
            Move(1, new Vector2(100f, 100f), StartTime + 0.1);
            Move(2, new Vector2(300f, 100f), StartTime + 0.15);
            Assert.That(_pinchTwists, Is.Empty);

            End(1, new Vector2(100f, 100f), StartTime + 0.2);
            End(2, new Vector2(300f, 100f), StartTime + 0.25);
            Begin(1, new Vector2(100f, 100f), StartTime + 0.3);
            Begin(2, new Vector2(200f, 100f), StartTime + 0.35);
            Move(2, new Vector2(300f, 100f), StartTime + 0.4);
            Assert.That(_pinchTwists.Count, Is.EqualTo(1));
            Assert.That(_pinchTwists[0].ScaleFactor, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Tick_PairStartsOutsideUi_CrossingUiDoesNotInterruptPinch()
        {
            CreateUi();
            Begin(1, new Vector2(100f, 100f), StartTime);
            Begin(2, new Vector2(200f, 100f), StartTime + 0.05);
            Move(1, new Vector2(20f, 100f), StartTime + 0.1);

            Assert.That(_pinchTwists.Count, Is.EqualTo(1));
            Assert.That(_pinchTwists[0].ScaleFactor, Is.EqualTo(1.8f).Within(1e-4f));
        }

        [Test]
        public void Tick_MouseLeftClick_PublishesTapWithoutDisablingMouse()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(100f, 100f), time: StartTime);
            Press(mouse.leftButton, time: StartTime + 0.01);
            _gestures.Tick();
            Release(mouse.leftButton, time: StartTime + 0.1);
            _gestures.Tick();

            Assert.That(mouse.enabled, Is.True);
            Assert.That(_taps, Is.EqualTo(new[] { new Vector2(100f, 100f) }));
            Assert.That(_dragPhases, Is.Empty);
        }

        [Test]
        public void Tick_MouseLeftDrag_PublishesDragPhases()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(100f, 100f), time: StartTime);
            Press(mouse.leftButton, time: StartTime + 0.01);
            _gestures.Tick();
            Set(mouse.position, new Vector2(200f, 100f), time: StartTime + 0.05);
            _gestures.Tick();
            Set(mouse.position, new Vector2(300f, 100f), time: StartTime + 0.1);
            _gestures.Tick();
            Release(mouse.leftButton, time: StartTime + 0.15);
            _gestures.Tick();

            Assert.That(_dragPhases, Is.EqualTo(new[] { DragPhase.Began, DragPhase.Moved, DragPhase.Ended }));
            Assert.That(_taps, Is.Empty);
        }

        [Test]
        public void Tick_MouseRightButton_RemainsAvailableForXrUnlockAndDoesNotPublishGestures()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            using (var unlock = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton"))
            {
                unlock.Enable();
                Press(mouse.rightButton, time: StartTime);
                _gestures.Tick();
                Set(mouse.position, new Vector2(300f, 300f), time: StartTime + 0.1);
                _gestures.Tick();

                Assert.That(mouse.enabled, Is.True);
                Assert.That(unlock.IsPressed(), Is.True);
                Assert.That(_taps, Is.Empty);
                Assert.That(_dragPhases, Is.Empty);
                Assert.That(_pinchTwists, Is.Empty);
            }
        }

        [Test]
        public void Tick_MouseStartsOverUi_DragAndTapAreBlocked()
        {
            CreateUi();
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(20f, 100f), time: StartTime);
            Press(mouse.leftButton, time: StartTime + 0.01);
            _gestures.Tick();
            Set(mouse.position, new Vector2(200f, 100f), time: StartTime + 0.05);
            _gestures.Tick();
            Release(mouse.leftButton, time: StartTime + 0.1);
            _gestures.Tick();

            Assert.That(_taps, Is.Empty);
            Assert.That(_dragPhases, Is.Empty);
        }

        [Test]
        public void Tick_MouseHeldBeyondTapDuration_DoesNotPublishTap()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton, time: StartTime);
            _gestures.Tick();
            Release(mouse.leftButton, time: StartTime + 1.0);
            _gestures.Tick();

            Assert.That(_taps, Is.Empty);
        }

        private void CreateUi()
        {
            _uiRoot = new GameObject("Gesture UI", typeof(EventSystem), typeof(GestureTestRaycaster));
            // UIBehaviour lifecycle callbacks do not run automatically in EditMode.
            SetUiLifecycle("OnEnable");
            EventSystem.current = _uiRoot.GetComponent<EventSystem>();
        }

        private void SetUiLifecycle(string method)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EventSystem).GetMethod(method, Flags).Invoke(_uiRoot.GetComponent<EventSystem>(), null);
            typeof(BaseRaycaster).GetMethod(method, Flags).Invoke(_uiRoot.GetComponent<GestureTestRaycaster>(), null);
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

    public sealed class GestureTestRaycaster : GraphicRaycaster
    {
        public override Camera eventCamera => null;

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (eventData.position.x < 50f)
            {
                resultAppendList.Add(new RaycastResult
                {
                    gameObject = gameObject,
                    module = this,
                    screenPosition = eventData.position
                });
            }
        }
    }
}
