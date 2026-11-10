using System;
using System.Collections.Generic;
using Game.Core.Configs;
using Game.Core.Hero;
using Game.Core.Match;
using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Net
{
    public sealed class NetworkPrefabFactory : IInitializable, IDisposable
    {
        private readonly IObjectResolver _resolver;
        private readonly NetworkPrefabList _catalog;
        private readonly Transform _arenaRoot;
        private readonly PudgeNetworkManager _manager;
        private readonly MatchSimulation _simulation;
        private readonly MatchStateSink _sink;
        private readonly ReconnectRegistry _reconnect;
        private readonly ArenaConfigData _arena;
        private readonly HeroConfigData _heroConfig;
        private readonly ServerPlayerCommands _commands;
        private readonly HashSet<GameObject> _registered = new();
        private readonly Dictionary<uint, GameObject> _prefabsById = new();
        private readonly HeroNet[] _players = new HeroNet[4];
        private MatchStateNet _matchState;
        private uint _nextSessionPlayerId = 1;
        private bool _initialized;
        private bool _disposed;

        public NetworkPrefabFactory(IObjectResolver resolver, NetworkPrefabList catalog,
            Transform arenaRoot, PudgeNetworkManager manager, MatchSimulation simulation,
            MatchStateSink sink, ReconnectRegistry reconnect, ArenaConfig arena, HeroConfig heroConfig,
            ServerPlayerCommands commands)
        {
            _resolver = resolver;
            _catalog = catalog;
            _arenaRoot = arenaRoot;
            _manager = manager;
            _simulation = simulation;
            _sink = sink;
            _reconnect = reconnect;
            _arena = arena.ToData();
            _heroConfig = heroConfig.ToData();
            _commands = commands;
        }

        public void Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetworkPrefabFactory));
            if (_initialized)
                return;
            _catalog.Validate();
            foreach (GameObject prefab in _catalog.Prefabs)
            {
                NetworkClient.RegisterPrefab(prefab,
                    SpawnClient, DestroyInstance);
                _registered.Add(prefab);
                _prefabsById.Add(prefab.GetComponent<NetworkIdentity>().assetId, prefab);
            }
            _initialized = true;
            _manager.AttachMatchFactory(this);
        }

        public GameObject SpawnClient(SpawnMessage message)
        {
            if (!_prefabsById.TryGetValue(message.assetId, out GameObject prefab))
                throw new ArgumentException("Unknown network prefab assetId.", nameof(message));
            GameObject instance = Create(prefab);
            instance.transform.localPosition = message.position;
            instance.transform.localRotation = message.rotation;
            instance.transform.localScale = message.scale;
            return instance;
        }

        private GameObject Create(GameObject prefab)
        {
            if (_disposed || !_initialized)
                throw new InvalidOperationException("Network prefab factory is not ready.");
            if (!_registered.Contains(prefab))
                throw new ArgumentException("Prefab is not registered in NetworkPrefabList.", nameof(prefab));
            return _resolver.Instantiate(prefab, _arenaRoot, false);
        }

        public NetworkIdentity SpawnServer(GameObject prefab)
        {
            RequireServer();
            GameObject instance = Create(prefab);
            try
            {
                NetworkServer.Spawn(instance);
                return instance.GetComponent<NetworkIdentity>();
            }
            catch
            {
                DestroyInstance(instance);
                throw;
            }
        }

        public void EnsureMatchState()
        {
            RequireServer();
            if (_matchState == null)
                _matchState = SpawnServer(_catalog.MatchState.gameObject).GetComponent<MatchStateNet>();
        }

        public HeroNet SpawnPlayer(NetworkConnectionToClient connection)
        {
            RequireServer();
            if (connection == null || !connection.isAuthenticated || !connection.isReady || connection.identity != null)
                throw new InvalidOperationException("A ready authenticated connection without a player is required.");

            int slot = Array.FindIndex(_players, player => player == null);
            if (slot < 0)
                throw new InvalidOperationException("All player slots are occupied.");
            if (_nextSessionPlayerId == 0)
                throw new InvalidOperationException("Session player IDs are exhausted.");

            GameObject instance = Create(_catalog.Hero.gameObject);
            uint playerId = _nextSessionPlayerId++;
            try
            {
                var hero = instance.GetComponent<HeroNet>();
                hero.InitializePlayer(playerId, slot);
                var points = hero.Team == Team.A ? _arena.TeamASpawnPoints : _arena.TeamBSpawnPoints;
                _simulation.AddHero(new HeroState
                {
                    Id = playerId, Team = hero.Team, Pos = points[slot / 2],
                    Radius = _heroConfig.Radius, Hp = _heroConfig.MaxHp, Alive = true
                });
                _sink.Register(hero);
                _simulation.TryGetHero(playerId, out HeroState state);
                hero.ApplyServerState(state);
                // AddPlayerForConnection performs the spawn and assigns the local player atomically.
                if (!NetworkServer.AddPlayerForConnection(connection, instance))
                    throw new InvalidOperationException("Mirror could not assign the player to the connection.");
                _players[slot] = hero;
                return hero;
            }
            catch
            {
                _sink.Unregister(playerId);
                _simulation.RemoveHero(playerId);
                if (instance.GetComponent<NetworkIdentity>().netId != 0)
                    NetworkServer.Destroy(instance);
                else
                    DestroyInstance(instance);
                throw;
            }
        }

        internal void DisconnectPlayer(NetworkConnectionToClient connection, bool reserve)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].connectionToClient == connection)
                {
                    uint id = _players[i].SessionPlayerId;
                    _simulation.StopMovement(id);
                    _commands.Remove(id);
                    NetworkServer.RemovePlayerForConnection(connection, RemovePlayerOptions.KeepActive);
                    if (reserve)
                        _reconnect.Reserve(id, NetworkTime.time);
                    else
                        RemovePlayer(id);
                    return;
                }
            }
        }

        internal void ExpireDisconnectedPlayers(double now) => _reconnect.Expire(now, RemovePlayer);

        private void RemovePlayer(uint id)
        {
            _reconnect.Remove(id);
            _commands.Remove(id);
            _simulation.RemoveHero(id);
            _sink.Unregister(id);
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || _players[i].SessionPlayerId != id)
                    continue;
                HeroNet hero = _players[i];
                _players[i] = null;
                NetworkServer.Destroy(hero.gameObject);
            }
        }

        internal void ClearSession()
        {
            _reconnect.Clear();
            for (int i = 0; i < _players.Length; i++)
                if (_players[i] != null)
                    RemovePlayer(_players[i].SessionPlayerId);
        }

        private static void RequireServer()
        {
            if (!NetworkServer.active)
                throw new InvalidOperationException("Only the active server may spawn network objects.");
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(instance);
            else
                UnityEngine.Object.DestroyImmediate(instance);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _manager.DetachMatchFactory(this);
            foreach (GameObject prefab in _registered)
                NetworkClient.UnregisterPrefab(prefab);
            _registered.Clear();
            _prefabsById.Clear();
        }
    }
}
