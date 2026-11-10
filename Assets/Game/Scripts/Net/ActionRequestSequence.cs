namespace Game.Net
{
    public sealed class ActionRequestSequence
    {
        private uint _epoch;
        private uint _lastRequestId;

        public void Reset(uint epoch)
        {
            _epoch = epoch;
            _lastRequestId = 0;
        }

        public bool TryConsume(uint requestId, uint epoch, out string reason)
        {
            reason = string.Empty;
            if (epoch == 0 || epoch != _epoch)
            {
                reason = "Stale session epoch";
                return false;
            }
            if (requestId == 0 || requestId <= _lastRequestId)
            {
                reason = "Stale request ID";
                return false;
            }
            _lastRequestId = requestId;
            return true;
        }
    }
}
