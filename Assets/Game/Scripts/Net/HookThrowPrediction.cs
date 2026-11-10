using Game.Core.Hook;
using UnityEngine;

namespace Game.Net
{
    public sealed class HookThrowPrediction
    {
        public const double TimeoutSeconds = 2;
        private uint _nextRequestId;
        private uint _epoch;
        private HookNetState _state;
        private double _deadline;

        public bool IsPending { get; private set; }
        public uint RequestId => _state.RequestId;

        public void Reset(uint epoch)
        {
            _epoch = epoch;
            _nextRequestId = 0;
            Cancel();
        }

        public void Cancel() => IsPending = false;

        public bool TryBegin(Vector2 origin, Vector2 direction, double now, out uint requestId)
        {
            requestId = 0;
            Expire(now);
            float lengthSquared = direction.sqrMagnitude;
            if (IsPending || _epoch == 0 || _nextRequestId == uint.MaxValue ||
                !Finite(origin.x) || !Finite(origin.y) || !Finite(lengthSquared) ||
                lengthSquared < 1e-12f || double.IsNaN(now) || double.IsInfinity(now))
                return false;
            requestId = ++_nextRequestId;
            _state = new HookNetState
            {
                Phase = HookPhase.Extending, Origin = origin, Dir = direction / Mathf.Sqrt(lengthSquared),
                StartTime = now, RequestId = requestId, SessionEpoch = _epoch
            };
            _deadline = now + TimeoutSeconds;
            IsPending = true;
            return true;
        }

        public void ApplyResult(uint requestId, uint epoch, bool accepted)
        {
            if (Matches(requestId, epoch) && !accepted)
                Cancel();
        }

        public bool TryGetState(double now, HookNetState authoritative, out HookNetState state)
        {
            Expire(now);
            if (Matches(authoritative.RequestId, authoritative.SessionEpoch))
                Cancel();
            state = IsPending ? _state : authoritative;
            return IsPending;
        }

        private bool Matches(uint requestId, uint epoch) => IsPending
            && requestId == _state.RequestId && epoch == _state.SessionEpoch;

        private void Expire(double now)
        {
            if (IsPending && now >= _deadline)
                Cancel();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
