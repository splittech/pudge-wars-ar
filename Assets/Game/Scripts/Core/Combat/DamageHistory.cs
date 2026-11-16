using System.Collections.Generic;

namespace Game.Core.Combat
{
    public sealed class DamageHistory
    {
        private readonly Dictionary<uint, Dictionary<uint, double>> _hits = new();

        public void Record(uint victim, uint attacker, double now)
        {
            if (!_hits.TryGetValue(victim, out var attackers))
                _hits.Add(victim, attackers = new Dictionary<uint, double>());
            attackers[attacker] = now;
        }

        public IEnumerable<uint> Assists(uint victim, uint killer, double now, double window)
        {
            if (!_hits.TryGetValue(victim, out var attackers)) yield break;
            foreach (var hit in attackers)
                if (hit.Key != killer && now - hit.Value <= window && now >= hit.Value)
                    yield return hit.Key;
        }

        public void Remove(uint victim) => _hits.Remove(victim);
        public void Clear() => _hits.Clear();
    }
}
