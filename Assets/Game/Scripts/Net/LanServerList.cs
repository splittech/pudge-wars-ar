using System;
using System.Collections.Generic;

namespace Game.Net
{
    public readonly struct LanServer
    {
        public readonly long Id;
        public readonly string Address;
        public readonly double LastSeen;

        public LanServer(long id, string address, double lastSeen)
        {
            Id = id;
            Address = address;
            LastSeen = lastSeen;
        }
    }

    public sealed class LanServerList
    {
        public const double ExpirySeconds = 10;
        public const int Capacity = 32;
        private readonly List<LanServer> _servers = new();
        public IReadOnlyList<LanServer> Servers => _servers;

        public void Clear() => _servers.Clear();

        public void Observe(long id, string address, double now)
        {
            if (!Ipv4Endpoint.TryValidate(address, out string validAddress) || !double.IsFinite(now))
                return;
            for (int i = 0; i < _servers.Count; i++)
            {
                if (_servers[i].Id != id)
                    continue;
                _servers[i] = new LanServer(id, validAddress, now);
                return;
            }
            if (_servers.Count < Capacity)
                _servers.Add(new LanServer(id, validAddress, now));
        }

        public void RemoveExpired(double now) =>
            _servers.RemoveAll(server => now - server.LastSeen >= ExpirySeconds);
    }
}
