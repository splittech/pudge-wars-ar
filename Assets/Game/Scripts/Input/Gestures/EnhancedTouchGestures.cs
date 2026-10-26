using System;
using System.Collections.Generic;
using Game.Core.Configs;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using VContainer.Unity;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Game.Input.Gestures
{
    /// <summary>
    /// Gesture recognizer over EnhancedTouch. In the editor the mouse is simulated as a touch (FR-IN-07).
    /// Tap and drag follow the first finger; a second finger cancels the drag and drives pinch/twist.
    /// Touches that start over UI are ignored (FR-IN-06).
    /// </summary>
    public sealed class EnhancedTouchGestures : ITouchGestures, ITickable, IDisposable
    {
        private const float ReferenceDpi = 160f;
        private const int NoTouch = -1;

        private readonly float _tapMaxTime;
        private readonly float _dragThresholdPixels;

        private readonly Subject<Vector2> _tap = new();
        private readonly Subject<DragGesture> _drag = new();
        private readonly Subject<PinchTwistGesture> _pinchTwist = new();

        private readonly List<RaycastResult> _uiRaycastResults = new();

        private int _primaryTouchId = NoTouch;
        private bool _isPrimaryIgnored;
        private bool _isDragging;

        private int _pinchFirstTouchId = NoTouch;
        private int _pinchSecondTouchId = NoTouch;
        private float _pinchDistance;
        private float _pinchAngle;

        public Observable<Vector2> Tap => _tap;
        public Observable<DragGesture> Drag => _drag;
        public Observable<PinchTwistGesture> PinchTwist => _pinchTwist;

        public EnhancedTouchGestures(InputConfig config)
        {
            InputConfigData data = config.ToData();

            // Screen.dpi is 0 when the platform cannot report it.
            float dpi = Screen.dpi > 0f ? Screen.dpi : ReferenceDpi;

            _tapMaxTime = data.TapMaxTime;
            _dragThresholdPixels = data.DragThreshold * dpi / ReferenceDpi;

            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // TouchSimulation is a hidden MonoBehaviour: created outside Play Mode (e.g. by EditMode tests)
            // it never receives OnEnable and stays broken in later Play Mode sessions.
            if (Application.isPlaying)
                TouchSimulation.Enable();
#endif
        }

        public void Tick()
        {
            ReadOnlyArray<Touch> touches = Touch.activeTouches;

            // activeTouches still contains touches that ended this frame, so their final phase is observed here.
            foreach (Touch touch in touches)
                ProcessPrimaryTouch(touch);

            ProcessPinchTwist(touches);
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                TouchSimulation.Disable();
#endif
            EnhancedTouchSupport.Disable();

            _tap.Dispose();
            _drag.Dispose();
            _pinchTwist.Dispose();
        }

        private void ProcessPrimaryTouch(Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
            {
                if (_primaryTouchId == NoTouch)
                    BeginPrimary(touch);
                else
                    IgnorePrimary(touch);

                return;
            }

            if (touch.touchId != _primaryTouchId)
                return;

            switch (touch.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    UpdatePrimary(touch);
                    break;

                case TouchPhase.Ended:
                    EndPrimary(touch);
                    break;

                case TouchPhase.Canceled:
                    IgnorePrimary(touch);
                    _primaryTouchId = NoTouch;
                    break;
            }
        }

        private void BeginPrimary(Touch touch)
        {
            _primaryTouchId = touch.touchId;
            _isPrimaryIgnored = IsOverUi(touch.screenPosition);
            _isDragging = false;
        }

        private void UpdatePrimary(Touch touch)
        {
            if (_isPrimaryIgnored)
                return;

            if (_isDragging)
            {
                if (touch.phase == TouchPhase.Moved)
                    _drag.OnNext(new DragGesture(DragPhase.Moved, touch.startScreenPosition, touch.screenPosition));

                return;
            }

            if (HasMovedBeyondThreshold(touch))
            {
                _isDragging = true;
                _drag.OnNext(new DragGesture(DragPhase.Began, touch.startScreenPosition, touch.screenPosition));
            }
        }

        private void EndPrimary(Touch touch)
        {
            if (!_isPrimaryIgnored)
            {
                if (_isDragging)
                    _drag.OnNext(new DragGesture(DragPhase.Ended, touch.startScreenPosition, touch.screenPosition));
                else if (touch.time - touch.startTime <= _tapMaxTime && !HasMovedBeyondThreshold(touch))
                    _tap.OnNext(touch.screenPosition);
            }

            _primaryTouchId = NoTouch;
        }

        /// <summary>
        /// Stops gesture recognition for the current primary touch; a running drag is cancelled.
        /// </summary>
        private void IgnorePrimary(Touch touch)
        {
            if (_isDragging && !_isPrimaryIgnored)
                _drag.OnNext(new DragGesture(DragPhase.Cancelled, touch.startScreenPosition, touch.screenPosition));

            _isPrimaryIgnored = true;
            _isDragging = false;
        }

        private void ProcessPinchTwist(ReadOnlyArray<Touch> touches)
        {
            if (touches.Count < 2 || !IsActive(touches[0]) || !IsActive(touches[1]))
            {
                _pinchFirstTouchId = NoTouch;
                _pinchSecondTouchId = NoTouch;
                return;
            }

            Touch first = touches[0];
            Touch second = touches[1];

            Vector2 offset = second.screenPosition - first.screenPosition;
            float distance = offset.magnitude;
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

            // Deltas are only meaningful against the previous frame of the same two fingers.
            bool isSamePair = first.touchId == _pinchFirstTouchId && second.touchId == _pinchSecondTouchId;

            if (isSamePair && _pinchDistance > 0f && distance > 0f)
            {
                float scaleFactor = distance / _pinchDistance;
                float angleDelta = Mathf.DeltaAngle(_pinchAngle, angle);

                if (!Mathf.Approximately(scaleFactor, 1f) || !Mathf.Approximately(angleDelta, 0f))
                    _pinchTwist.OnNext(new PinchTwistGesture(scaleFactor, angleDelta));
            }

            _pinchFirstTouchId = first.touchId;
            _pinchSecondTouchId = second.touchId;
            _pinchDistance = distance;
            _pinchAngle = angle;
        }

        private bool HasMovedBeyondThreshold(Touch touch)
        {
            return (touch.screenPosition - touch.startScreenPosition).sqrMagnitude
                > _dragThresholdPixels * _dragThresholdPixels;
        }

        private static bool IsActive(Touch touch)
        {
            return touch.phase is not (TouchPhase.Ended or TouchPhase.Canceled);
        }

        private bool IsOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
                return false;

            PointerEventData pointerData = new(eventSystem) { position = screenPosition };
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);

            // Only UI graphics count: a PhysicsRaycaster on the camera would also report arena objects.
            bool isOverUi = _uiRaycastResults.Exists(result => result.module is GraphicRaycaster);
            _uiRaycastResults.Clear();

            return isOverUi;
        }
    }
}
