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
    /// Gesture recognizer over EnhancedTouch. In the editor LMB follows the same tap/drag rules (FR-IN-07).
    /// Tap and drag follow the first finger; a second finger cancels the drag and drives pinch/twist.
    /// Touches that start over UI are ignored (FR-IN-06).
    /// </summary>
    public sealed class EnhancedTouchGestures : ITouchGestures, ITickable, IDisposable
    {
        private const float ReferenceDpi = 160f;
        private const int NoTouch = -1;
#if UNITY_EDITOR
        private const int EditorMouseId = int.MinValue;
#endif

        private readonly float _tapMaxTime;
        private readonly float _dragThresholdPixels;

        private readonly Subject<Vector2> _tap = new();
        private readonly Subject<DragGesture> _drag = new();
        private readonly Subject<PinchTwistGesture> _pinchTwist = new();

        private readonly List<RaycastResult> _uiRaycastResults = new();
        private readonly HashSet<int> _uiTouchIds = new();

        private int _primaryTouchId = NoTouch;
        private bool _isPrimaryIgnored;
        private bool _isDragging;

        private int _pinchFirstTouchId = NoTouch;
        private int _pinchSecondTouchId = NoTouch;
        private float _pinchDistance;
        private float _pinchAngle;
#if UNITY_EDITOR
        private Vector2 _mouseStartPosition;
        private Vector2 _mousePreviousPosition;
        private double _mouseStartTime;
#endif

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
        }

        public void Tick()
        {
            ReadOnlyArray<Touch> touches = Touch.activeTouches;

            // activeTouches still contains touches that ended this frame, so their final phase is observed here.
            foreach (Touch touch in touches)
            {
                ProcessPrimaryTouch(touch);
            }

            ProcessPinchTwist(touches);

            foreach (Touch touch in touches)
            {
                if (!IsActive(touch))
                {
                    _uiTouchIds.Remove(touch.touchId);
                }
            }

#if UNITY_EDITOR
            ProcessEditorMouse();
#endif
        }

        public void Dispose()
        {
            EnhancedTouchSupport.Disable();

            _tap.Dispose();
            _drag.Dispose();
            _pinchTwist.Dispose();
        }

        private void ProcessPrimaryTouch(Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
            {
                if (IsOverUi(touch.screenPosition))
                {
                    _uiTouchIds.Add(touch.touchId);
                }

                if (_primaryTouchId == NoTouch)
                {
                    BeginPrimary(touch.touchId, _uiTouchIds.Contains(touch.touchId));
                }
                else
                {
                    IgnorePrimary(touch.startScreenPosition, touch.screenPosition);
                }

                return;
            }

            if (touch.touchId != _primaryTouchId)
            {
                return;
            }

            switch (touch.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    UpdatePrimary(touch.startScreenPosition, touch.screenPosition, touch.phase == TouchPhase.Moved);
                    break;

                case TouchPhase.Ended:
                    EndPrimary(touch.startScreenPosition, touch.screenPosition, touch.time - touch.startTime);
                    break;

                case TouchPhase.Canceled:
                    IgnorePrimary(touch.startScreenPosition, touch.screenPosition);
                    _primaryTouchId = NoTouch;
                    break;
            }
        }

        private void BeginPrimary(int touchId, bool ignored)
        {
            _primaryTouchId = touchId;
            _isPrimaryIgnored = ignored;
            _isDragging = false;
        }

        private void UpdatePrimary(Vector2 startPosition, Vector2 position, bool moved)
        {
            if (_isPrimaryIgnored)
            {
                return;
            }

            if (_isDragging)
            {
                if (moved)
                {
                    _drag.OnNext(new DragGesture(DragPhase.Moved, startPosition, position));
                }

                return;
            }

            if (HasMovedBeyondThreshold(startPosition, position))
            {
                _isDragging = true;
                _drag.OnNext(new DragGesture(DragPhase.Began, startPosition, position));
            }
        }

        private void EndPrimary(Vector2 startPosition, Vector2 position, double duration)
        {
            if (!_isPrimaryIgnored)
            {
                if (_isDragging)
                {
                    _drag.OnNext(new DragGesture(DragPhase.Ended, startPosition, position));
                }
                else if (duration <= _tapMaxTime && !HasMovedBeyondThreshold(startPosition, position))
                {
                    _tap.OnNext(position);
                }
            }

            _primaryTouchId = NoTouch;
        }

        /// <summary>
        /// Stops gesture recognition for the current primary touch; a running drag is cancelled.
        /// </summary>
        private void IgnorePrimary(Vector2 startPosition, Vector2 position)
        {
            if (_isDragging && !_isPrimaryIgnored)
            {
                _drag.OnNext(new DragGesture(DragPhase.Cancelled, startPosition, position));
            }

            _isPrimaryIgnored = true;
            _isDragging = false;
        }

#if UNITY_EDITOR
        private void ProcessEditorMouse()
        {
            // TouchSimulation disables Mouse, which also disables XR Simulation's RMB unlock action.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null)
            {
                if (_primaryTouchId == EditorMouseId)
                {
                    IgnorePrimary(_mouseStartPosition, _mousePreviousPosition);
                    _primaryTouchId = NoTouch;
                }

                return;
            }

            Vector2 position = mouse.position.ReadValue();
            if (_primaryTouchId == NoTouch && mouse.leftButton.isPressed && mouse.leftButton.wasPressedThisFrame)
            {
                _mouseStartPosition = position;
                _mousePreviousPosition = position;
                _mouseStartTime = mouse.lastUpdateTime;
                BeginPrimary(EditorMouseId, IsOverUi(position));
                return;
            }

            if (_primaryTouchId != EditorMouseId)
            {
                return;
            }

            if (mouse.leftButton.isPressed)
            {
                UpdatePrimary(_mouseStartPosition, position, position != _mousePreviousPosition);
            }
            else
            {
                EndPrimary(_mouseStartPosition, position, mouse.lastUpdateTime - _mouseStartTime);
            }

            _mousePreviousPosition = position;
        }
#endif

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

            if (_uiTouchIds.Contains(first.touchId) || _uiTouchIds.Contains(second.touchId))
            {
                _pinchFirstTouchId = NoTouch;
                _pinchSecondTouchId = NoTouch;
                return;
            }

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
                {
                    _pinchTwist.OnNext(new PinchTwistGesture(scaleFactor, angleDelta));
                }
            }

            _pinchFirstTouchId = first.touchId;
            _pinchSecondTouchId = second.touchId;
            _pinchDistance = distance;
            _pinchAngle = angle;
        }

        private bool HasMovedBeyondThreshold(Vector2 startPosition, Vector2 position)
        {
            return (position - startPosition).sqrMagnitude
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
            {
                return false;
            }

            PointerEventData pointerData = new(eventSystem)
            {
                position = screenPosition
            };
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);

            // Only UI raycasters count; physics hits must not suppress arena gestures.
            bool isOverUi = _uiRaycastResults.Exists(result => result.module is GraphicRaycaster || result.module is UnityEngine.UIElements.PanelRaycaster);
            _uiRaycastResults.Clear();

            return isOverUi;
        }
    }
}
