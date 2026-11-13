using System;
using UnityEngine;

namespace Game.Net
{
    public sealed class MovementLatencyProbe
    {
        private bool _waiting;
        private Vector2 _origin;
        private double _start;
        public void Reset() => _waiting = false;
        public void Begin(Vector2 position, Vector2 previousTarget, Vector2 target, bool canMove, bool settled, double now)
        {
            // Starting from rest avoids mistaking pre-existing motion for a response to a new tap.
            _waiting = canMove && settled && double.IsFinite(now) && SessionSerialization.Finite(target)
                && (position - previousTarget).sqrMagnitude < 0.000001f && (target - position).sqrMagnitude > 0.000001f;
            _origin = position; _start = now;
        }
        public bool TryPresent(Vector2 position, bool canMove, double now, out double milliseconds)
        {
            milliseconds = double.NaN;
            if (!canMove) _waiting = false;
            if (!_waiting || !double.IsFinite(now) || now < _start || !SessionSerialization.Finite(position)
                || (position - _origin).sqrMagnitude < 0.000001f) return false;
            milliseconds = (now - _start) * 1000;
            _waiting = false;
            return true;
        }
    }
}
