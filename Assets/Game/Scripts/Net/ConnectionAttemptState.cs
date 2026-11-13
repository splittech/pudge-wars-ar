namespace Game.Net
{
    public sealed class ConnectionAttemptState
    {
        public long Id { get; private set; }
        public bool IsPending { get; private set; }
        public bool IsConnected { get; private set; }
        public bool HostDisconnected { get; private set; }
        public string Message { get; private set; } = string.Empty;

        public long Begin(bool host)
        {
            Id++;
            IsPending = true;
            IsConnected = false;
            Message = host ? "Creating game..." : "Connecting...";
            return Id;
        }

        public bool Complete(long id)
        {
            if (!IsPending || id != Id)
                return false;
            IsPending = false;
            IsConnected = true;
            Message = "Connected. Loading arena...";
            return true;
        }

        public bool Fail(long id, string message)
        {
            if (!IsPending || id != Id)
                return false;
            IsPending = false;
            IsConnected = false;
            Message = message;
            return true;
        }

        public void End(bool expected)
        {
            if (!IsPending && !IsConnected)
                return;
            if (!expected && IsConnected)
                HostDisconnected = true;
            Message = expected ? "Session ended." : IsConnected
                ? "Connection closed by host." : "Could not connect. Check the IP and Wi-Fi.";
            IsPending = false;
            IsConnected = false;
        }

        public void ShowValidationError(string message)
        {
            if (!IsPending && !IsConnected)
                Message = message;
        }

        public void AcknowledgeHostDisconnected()
        {
            HostDisconnected = false;
            ShowValidationError(string.Empty);
        }
    }
}
