using System;
using System.Collections.Generic;

namespace Game.Net
{
    public sealed class ReconnectRegistry
    {
        public const double GracePeriod = 30;
        private readonly Dictionary<uint, double> _deadlines = new(4);
        private readonly List<uint> _expired = new(4);

        public int Count => _deadlines.Count;

        public void Reserve(uint playerId, double now)
        {
            if (playerId == 0 || double.IsNaN(now) || double.IsInfinity(now))
                throw new ArgumentOutOfRangeException(nameof(playerId));
            // Duplicate disconnect callbacks must not extend the reservation.
            _deadlines.TryAdd(playerId, now + GracePeriod);
        }

        public void Remove(uint playerId) => _deadlines.Remove(playerId);
        public void Clear() => _deadlines.Clear();

        public void Expire(double now, Action<uint> removePlayer)
        {
            _expired.Clear();
            foreach (var entry in _deadlines)
                if (now >= entry.Value)
                    _expired.Add(entry.Key);
            foreach (uint id in _expired)
            {
                _deadlines.Remove(id);
                removePlayer(id);
            }
        }
    }
}
