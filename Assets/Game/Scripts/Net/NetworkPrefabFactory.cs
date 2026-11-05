using System;
using System.Collections.Generic;
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
        private readonly HashSet<GameObject> _registered = new();
        private readonly Dictionary<uint, GameObject> _prefabsById = new();
        private readonly HeroNet[] _players = new HeroNet[4];
        private MatchStateNet _matchState;
        private uint _nextSessionPlayerId = 1;
        private bool _initialized;
        private bool _disposed;

        public NetworkPrefabFactory(IObjectResolver resolver, NetworkPrefabList catalog,
            Transform arenaRoot, PudgeNetworkManager manager)
        {
            _resolver = resolver;
            _catalog = catalog;
            _arenaRoot = arenaRoot;
            _manager = manager;
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
            try
            {
                var hero = instance.GetComponent<HeroNet>();
                hero.InitializePlayer(_nextSessionPlayerId++, slot);
                // AddPlayerForConnection performs the spawn and assigns the local player atomically.
                if (!NetworkServer.AddPlayerForConnection(connection, instance))
                    throw new InvalidOperationException("Mirror could not assign the player to the connection.");
                _players[slot] = hero;
                return hero;
            }
            catch
            {
                if (instance.GetComponent<NetworkIdentity>().netId != 0)
                    NetworkServer.Destroy(instance);
                else
                    DestroyInstance(instance);
                throw;
            }
        }

        internal void ReleasePlayer(NetworkConnectionToClient connection)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].connectionToClient == connection)
                    _players[i] = null;
            }
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
