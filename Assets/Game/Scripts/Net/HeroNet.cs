using System;
using Game.Core.Configs;
using Game.Core.Match;
using Game.Core.Hero;
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
        [SyncVar] private int _hp;
        [SyncVar] private bool _alive;
        [SyncVar] private bool _isPulled;
        [SyncVar] private double _hookCooldownEnd;
        private ArenaConfigData _arena;

        public uint SessionPlayerId => _sessionPlayerId;
        public int Slot => _slot;
        public Team Team => _team;
        public int Hp => _hp;
        public bool Alive => _alive;
        public bool IsPulled => _isPulled;
        public double HookCooldownEnd => _hookCooldownEnd;
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

        internal void ApplyServerState(HeroState state)
        {
            if (state.Id != _sessionPlayerId)
                throw new ArgumentException("Simulation player ID does not match the network hero.", nameof(state));
            Vector3 position = transform.localPosition;
            transform.localPosition = new Vector3(state.Pos.x, position.y, state.Pos.y);
            _hp = state.Hp;
            _alive = state.Alive;
            _isPulled = state.IsPulled;
            _hookCooldownEnd = state.HookCooldownEnd;
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
