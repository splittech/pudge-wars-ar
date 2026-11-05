using Game.Input.Gestures;
using Game.Input.Routing;
using Game.Presentation;
using Game.Net;
using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.App
{
    public class GameplayScope : LifetimeScope
    {
        [SerializeField] private OfflineSimulationDriver _offlineDriver;
        [SerializeField] private FlatArenaSpace _arenaSpace;
        [SerializeField] private PlayableArenaView _arenaView;
        [SerializeField] private Transform _arenaRoot;

        protected override void Configure(IContainerBuilder builder)
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                builder.RegisterInstance(_arenaRoot);
                builder.RegisterEntryPoint<NetworkPrefabFactory>().AsSelf();
            }
            // Input. RegisterEntryPoint binds all implemented interfaces, including ITouchGestures.
            builder.RegisterEntryPoint<EnhancedTouchGestures>();
            builder.RegisterEntryPoint<TouchGesturesDebugLogger>();
            builder.RegisterComponent(_offlineDriver).AsSelf().AsImplementedInterfaces();
            builder.RegisterComponent(_arenaSpace).As<IArenaSpace>();
            builder.RegisterEntryPoint<InputRouter>().AsSelf();
            builder.RegisterComponent(_arenaView);
        }
    }
}
