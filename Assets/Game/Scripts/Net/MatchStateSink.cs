using System.Collections.Generic;
using Game.Core.Match;

namespace Game.Net
{
    public sealed class MatchStateSink : IMatchStateSink
    {
        private readonly Dictionary<uint, HeroNet> _heroes = new(4);

        public void Register(HeroNet hero) => _heroes.Add(hero.SessionPlayerId, hero);
        public void Unregister(uint playerId) => _heroes.Remove(playerId);

        public void Apply(MatchSimulation simulation)
        {
            for (int i = 0; i < simulation.HeroCount; i++)
            {
                var state = simulation.GetHero(i);
                if (_heroes.TryGetValue(state.Id, out HeroNet hero) && hero != null)
                    hero.ApplyServerState(state);
            }
        }
    }
}
