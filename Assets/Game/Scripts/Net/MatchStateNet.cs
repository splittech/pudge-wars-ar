using System;
using Game.Core.Configs;
using Mirror;
using VContainer;

namespace Game.Net
{
    public sealed class MatchStateNet : NetworkBehaviour
    {
        public readonly SyncList<PlayerInfo> Players = new();
        public bool HasDependencies { get; private set; }
        public bool ClientStartedAfterInjection { get; private set; }

        [Inject]
        public void Construct(MatchConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            HasDependencies = true;
        }

        public override void OnStartServer() => RequireDependencies();

        public override void OnStartClient()
        {
            RequireDependencies();
            ClientStartedAfterInjection = true;
        }

        private void RequireDependencies()
        {
            if (!HasDependencies)
                throw new InvalidOperationException("MatchStateNet must be created through NetworkPrefabFactory before network callbacks.");
        }
    }
}
