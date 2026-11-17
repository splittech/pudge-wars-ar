using System.Collections.Generic;

namespace Game.Core.Match
{
    public sealed class PlacementReadiness
    {
        private readonly HashSet<uint> _pending = new();
        public double Deadline { get; private set; }
        public bool Complete => _pending.Count == 0;
        public IEnumerable<uint> Pending => _pending;

        public void Begin(IEnumerable<uint> players, double now, float timeout)
        {
            _pending.Clear();
            foreach (uint id in players) _pending.Add(id);
            Deadline = now + timeout;
        }

        public bool Confirm(uint id, double now) => now < Deadline && _pending.Remove(id);
        public void Remove(uint id) => _pending.Remove(id);
    }
}
