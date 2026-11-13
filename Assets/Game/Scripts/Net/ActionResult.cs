namespace Game.Net
{
    public enum ActionKind : byte
    {
        Throw, BuyUpgrade
    }

    public readonly struct ActionResult
    {
        public readonly ActionKind Kind;
        public readonly uint RequestId;
        public readonly uint SessionEpoch;
        public readonly bool Accepted;
        public readonly string Reason;

        public ActionResult(ActionKind kind, uint requestId, uint epoch, bool accepted, string reason)
        {
            Kind = kind;
            RequestId = requestId;
            SessionEpoch = epoch;
            Accepted = accepted;
            Reason = reason;
        }
    }
}
