using Game.Input.Gestures;
using Game.Input.Routing;
using Game.Presentation;
using Game.Net;
using Game.Core.Configs;
using Game.Core.Hero;
using Game.Core.Match;
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
                builder.Register(resolver => new MatchSimulation(
                    resolver.Resolve<ArenaConfig>().ToData(), resolver.Resolve<HeroConfig>().ToData(),
                    resolver.Resolve<HookConfig>().ToData(), System.Array.Empty<HeroState>()), Lifetime.Scoped);
                builder.Register<MatchStateSink>(Lifetime.Scoped).AsSelf().As<IMatchStateSink>();
                builder.Register<ReconnectRegistry>(Lifetime.Scoped);
                builder.RegisterEntryPoint<NetworkPrefabFactory>().AsSelf();
                builder.RegisterEntryPoint<ServerTickRunner>().AsSelf();
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
