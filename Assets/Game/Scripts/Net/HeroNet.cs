using System;
using Game.Core.Configs;
using Game.Core.Match;
using Mirror;
using UnityEngine;
using VContainer;

namespace Game.Net
{
    public sealed class HeroNet : NetworkBehaviour
    {
        [SyncVar] private uint _sessionPlayerId;
        [SyncVar] private int _slot = -1;
        [SyncVar] private Team _team;
        private ArenaConfigData _arena;

        public uint SessionPlayerId => _sessionPlayerId;
        public int Slot => _slot;
        public Team Team => _team;
        public bool IsLocalHero => isLocalPlayer;
        public bool HasDependencies { get; private set; }
        public bool ClientStartedAfterInjection { get; private set; }

        [Inject]
        public void Construct(ArenaConfig arena)
        {
            _arena = arena.ToData();
            HasDependencies = true;
        }

        internal void InitializePlayer(uint sessionPlayerId, int slot)
        {
            RequireDependencies();
            if (sessionPlayerId == 0 || slot < 0 || slot >= 4)
                throw new ArgumentOutOfRangeException(nameof(slot), "A nonzero session ID and a slot in 0..3 are required.");

            Team team = slot % 2 == 0 ? Team.A : Team.B;
            var points = team == Team.A ? _arena.TeamASpawnPoints : _arena.TeamBSpawnPoints;
            int index = slot / 2;
            if (index >= points.Count)
                throw new InvalidOperationException("ArenaConfig does not contain enough spawn points.");

            _sessionPlayerId = sessionPlayerId;
            _slot = slot;
            _team = team;
            transform.localPosition = new Vector3(points[index].x, 0f, points[index].y);
        }

        public override void OnStartServer()
        {
            RequireDependencies();
        }

        public override void OnStartClient()
        {
            RequireDependencies();
            ClientStartedAfterInjection = true;
            gameObject.name = $"Hero {_sessionPlayerId} ({_team})";
        }

        private void RequireDependencies()
        {
            if (!HasDependencies)
                throw new InvalidOperationException("HeroNet must be created through NetworkPrefabFactory before network callbacks.");
        }
    }
}
