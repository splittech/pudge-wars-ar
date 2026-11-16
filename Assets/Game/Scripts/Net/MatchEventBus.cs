using System;
using R3;

namespace Game.Net
{
    public struct KillNotice
    {
        public uint KillerId;
        public uint VictimId;
    }

    public sealed class MatchEventBus : IDisposable
    {
        private readonly Subject<KillNotice> _kills = new();
        public Observable<KillNotice> Kills => _kills;
        public void Publish(KillNotice notice) => _kills.OnNext(notice);
        public void Dispose() => _kills.Dispose();
    }
}
