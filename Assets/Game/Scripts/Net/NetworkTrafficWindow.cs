using System;

namespace Game.Net
{
    public readonly struct NetworkTrafficSample
    {
        public readonly double MessageBytesIn, MessageBytesOut, MessagesIn, MessagesOut;
        public readonly double PayloadBytesIn, PayloadBytesOut, BatchesIn, BatchesOut;
        public NetworkTrafficSample(double seconds, long bytesIn, long bytesOut, long messagesIn,
            long messagesOut, long payloadIn, long payloadOut, long batchesIn, long batchesOut)
        {
            MessageBytesIn = bytesIn / seconds; MessageBytesOut = bytesOut / seconds;
            MessagesIn = messagesIn / seconds; MessagesOut = messagesOut / seconds;
            PayloadBytesIn = payloadIn / seconds; PayloadBytesOut = payloadOut / seconds;
            BatchesIn = batchesIn / seconds; BatchesOut = batchesOut / seconds;
        }
    }

    public sealed class NetworkTrafficWindow
    {
        private long _bytesIn, _bytesOut, _messagesIn, _messagesOut;
        private long _payloadIn, _payloadOut, _batchesIn, _batchesOut;
        public void Message(bool incoming, int bytes, int recipients = 1)
        {
            if (bytes < 0 || recipients < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            if (incoming) { _bytesIn += (long)bytes * recipients; _messagesIn += recipients; }
            else { _bytesOut += (long)bytes * recipients; _messagesOut += recipients; }
        }
        public void Payload(bool incoming, int bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            if (incoming) { _payloadIn += bytes; _batchesIn++; }
            else { _payloadOut += bytes; _batchesOut++; }
        }
        public void Clear()
        {
            _bytesIn = _bytesOut = _messagesIn = _messagesOut = 0;
            _payloadIn = _payloadOut = _batchesIn = _batchesOut = 0;
        }
        public NetworkTrafficSample Take(double seconds)
        {
            if (!double.IsFinite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            var result = new NetworkTrafficSample(seconds, _bytesIn, _bytesOut, _messagesIn,
                _messagesOut, _payloadIn, _payloadOut, _batchesIn, _batchesOut);
            Clear();
            return result;
        }
    }
}
