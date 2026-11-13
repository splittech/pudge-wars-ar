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
            _manager.Roster.Changed += SynchronizeRoster;
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
            {
                _matchState = SpawnServer(_catalog.MatchState.gameObject).GetComponent<MatchStateNet>();
                SynchronizeRoster();
            }
        }

        private void SynchronizeRoster()
        {
            if (_matchState == null || !NetworkServer.active)
                return;
            _matchState.Players.Clear();
            foreach (PlayerInfo player in _manager.Roster.Players)
                _matchState.Players.Add(player);
        }

        public HeroNet SpawnPlayer(NetworkConnectionToClient connection)
        {
            RequireServer();
            if (connection == null || !connection.isAuthenticated || !connection.isReady || connection.identity != null)
                throw new InvalidOperationException("A ready authenticated connection without a player is required.");

            var admission = connection.authenticationData as SessionPlayer;
            if (!_manager.Roster.Contains(admission) || !admission.Info.Connected)
                throw new InvalidOperationException("Handshake must reserve an identity before player spawn.");
            int slot = admission.Slot;
            if (admission.HasHero)
            {
                var existing = _players[slot];
                if (existing == null || existing.connectionToClient != null)
                    throw new InvalidOperationException("Reserved hero is unavailable or already owned.");
                existing.RestoreSession(admission.Epoch);
                if (!NetworkServer.AddPlayerForConnection(connection, existing.gameObject))
                    throw new InvalidOperationException("Could not restore player ownership.");
                admission.Connection = connection;
                _manager.Roster.CompleteRestore(admission);
                _reconnect.Remove(admission.Info.SessionPlayerId);
                return existing;
            }
            if (_players[slot] != null)
                throw new InvalidOperationException("Authenticated slot already has a hero.");

            GameObject instance = Create(_catalog.Hero.gameObject);
            uint playerId = admission.Info.SessionPlayerId;
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
                _manager.Roster.MarkSpawned(admission);
                admission.Connection = connection;
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
            if (connection.authenticationData is SessionPlayer bound && bound.Connection != null
                && !ReferenceEquals(bound.Connection, connection))
                return;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].connectionToClient == connection)
                {
                    uint id = _players[i].SessionPlayerId;
                    _simulation.StopMovement(id);
                    _commands.Remove(id);
                    NetworkServer.RemovePlayerForConnection(connection, RemovePlayerOptions.KeepActive);
                    if (reserve)
                    {
                        _reconnect.Reserve(id, NetworkTime.time);
                        _manager.Roster.Disconnect(connection.authenticationData as SessionPlayer, true);
                    }
                    else
                        RemovePlayer(id);
                    return;
                }
            }
            if (connection.authenticationData is SessionPlayer pending && _manager.Roster.Contains(pending) && pending.HasHero)
            {
                if (reserve)
                    _reconnect.Reserve(pending.Info.SessionPlayerId, NetworkTime.time);
                else
                    RemovePlayer(pending.Info.SessionPlayerId);
            }
        }

        internal void ExpireDisconnectedPlayers(double now) => _reconnect.Expire(now, id =>
        {
            foreach (var info in _manager.Roster.Players)
                if (info.SessionPlayerId == id && info.Connected)
                    return;
            RemovePlayer(id);
        });

        private void RemovePlayer(uint id)
        {
            _reconnect.Remove(id);
            _manager.Roster.Remove(id);
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
            _manager.Roster.Clear();
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
            _manager.Roster.Changed -= SynchronizeRoster;
            foreach (GameObject prefab in _registered)
                NetworkClient.UnregisterPrefab(prefab);
            _registered.Clear();
            _prefabsById.Clear();
        }
    }
}
