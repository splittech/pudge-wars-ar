using System;
using Mirror.Discovery;
using UnityEngine;

namespace Game.Net
{
    [DisallowMultipleComponent]
    public sealed class LanDiscovery : NetworkDiscovery
    {
        // Stable across installations; the authenticator checks the wire protocol separately.
        public const long GameHandshake = 0x50574441524C414E;
        private readonly LanServerList _servers = new();
        public LanServerList Results => _servers;
        public bool IsSearching { get; private set; }

        private void Awake()
        {
            secretHandshake = GameHandshake;
            OnServerFound ??= new ServerFoundUnityEvent<ServerResponse>();
            OnServerFound.AddListener(OnFound);
        }

#if UNITY_EDITOR
        public override void OnValidate()
        {
            secretHandshake = GameHandshake;
            base.OnValidate();
        }
#endif

        public void Search()
        {
            _servers.Clear();
            IsSearching = false;
            try
            {
                StartDiscovery();
                IsSearching = true;
            }
            catch
            {
                StopDiscovery();
                throw;
            }
        }

        public void StopSearch()
        {
            IsSearching = false;
            StopDiscovery();
            _servers.Clear();
        }

        private void Update()
        {
            if (IsSearching)
                _servers.RemoveExpired(Time.realtimeSinceStartupAsDouble);
        }

        private void OnFound(ServerResponse response)
        {
            if (!IsSearching || response.uri == null || response.uri.Scheme != "kcp" || response.uri.Port != 7777)
                return;
            _servers.Observe(response.serverId, response.EndPoint.Address.ToString(), Time.realtimeSinceStartupAsDouble);
        }
    }
}
