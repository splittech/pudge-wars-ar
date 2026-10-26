using R3;
using UnityEngine;

namespace Game.Input.Gestures
{
    /// <summary>
    /// Screen-space touch gestures. Positions are in screen pixels.
    /// </summary>
    public interface ITouchGestures
    {
        /// <summary>
        /// Short touch without noticeable movement; emits the screen point.
        /// </summary>
        Observable<Vector2> Tap { get; }

        /// <summary>
        /// One-finger drag with Began / Moved / Ended / Cancelled phases.
        /// </summary>
        Observable<DragGesture> Drag { get; }

        /// <summary>
        /// Two-finger scale and rotation changes.
        /// </summary>
        Observable<PinchTwistGesture> PinchTwist { get; }
    }
}
