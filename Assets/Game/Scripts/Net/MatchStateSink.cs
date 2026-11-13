using System.Collections.Generic;
using Game.Core.Match;
using UnityEngine;

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
                {
                    hero.ApplyServerState(state);
                    hero.PublishMovement(simulation.TickCount, simulation.Time, state);
                    var hook = simulation.GetHook(i);
                    uint targetNetId = _heroes.TryGetValue(hook.TargetId, out HeroNet target) && target != null
                        ? target.netId : 0;
                    hero.ApplyServerHook(HookNetState.FromSimulation(hook, targetNetId));
                }
            }
            foreach (var hit in simulation.Hits)
                if (_heroes.TryGetValue(hit.OwnerId, out HeroNet owner) && owner != null)
                    owner.RpcHookHit(new Vector2(hit.Position.x, hit.Position.y));
            foreach (var clash in simulation.Clashes)
            {
                Vector2 position = new(clash.Position.x, clash.Position.y);
                if (_heroes.TryGetValue(clash.FirstOwnerId, out HeroNet first) && first != null)
                    first.RpcHookClash(position);
                if (_heroes.TryGetValue(clash.SecondOwnerId, out HeroNet second) && second != null)
                    second.RpcHookClash(position);
            }
        }
    }
}
