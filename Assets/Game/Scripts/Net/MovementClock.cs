using System;

namespace Game.Net
{
    public sealed class MovementClock
    {
        private bool _started;
        private double _next;
        public bool DroppedTime { get; private set; }
        public void Reset() { _started = false; _next = 0; DroppedTime = false; }
        public int Advance(double now)
        {
            if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
            DroppedTime = false;
            if (!_started) { _next = now; _started = true; }
            int steps = 0;
            while (steps < 4 && now + 1e-9 >= _next)
            {
                steps++;
                _next += MovementPredictor.Delta;
            }
            if (now - _next > 4 * MovementPredictor.Delta)
            {
                _next = now + MovementPredictor.Delta;
                DroppedTime = true;
            }
            return steps;
        }
    }
}
