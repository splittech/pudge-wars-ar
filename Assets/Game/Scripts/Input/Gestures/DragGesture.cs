using UnityEngine;

namespace Game.Input.Gestures
{
    public enum DragPhase
    {
        Began,
        Moved,
        Ended,
        Cancelled
    }

    public readonly struct DragGesture
    {
        public readonly DragPhase Phase;
        public readonly Vector2 StartPosition;
        public readonly Vector2 Position;

        public DragGesture(DragPhase phase, Vector2 startPosition, Vector2 position)
        {
            Phase = phase;
            StartPosition = startPosition;
            Position = position;
        }
    }
}
