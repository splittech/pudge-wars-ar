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
                builder.Register(resolver =>
                {
                    var simulation = new MatchSimulation(resolver.Resolve<ArenaConfig>().ToData(),
                        resolver.Resolve<HeroConfig>().ToData(), resolver.Resolve<HookConfig>().ToData(),
                        System.Array.Empty<HeroState>());
                    simulation.ConfigureRules(resolver.Resolve<MatchConfig>().ToData(), resolver.Resolve<EconomyConfig>().ToData());
                    return simulation;
                }, Lifetime.Scoped);
                builder.Register<MatchStateSink>(Lifetime.Scoped).AsSelf().As<IMatchStateSink>();
                builder.Register<ReconnectRegistry>(Lifetime.Scoped);
                builder.Register<LocalPlayerCommands>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
                builder.Register(resolver => new ServerPlayerCommands(resolver.Resolve<MatchSimulation>(),
                    resolver.Resolve<ArenaConfig>()), Lifetime.Scoped);
                builder.RegisterEntryPoint<NetworkPrefabFactory>().AsSelf();
                builder.RegisterEntryPoint<ServerTickRunner>().AsSelf();
            }
            // Input. RegisterEntryPoint binds all implemented interfaces, including ITouchGestures.
            builder.RegisterEntryPoint<EnhancedTouchGestures>();
            builder.RegisterEntryPoint<TouchGesturesDebugLogger>();
            if (NetworkServer.active || NetworkClient.active)
                builder.RegisterComponent(_offlineDriver).AsSelf();
            else
                builder.RegisterComponent(_offlineDriver).AsSelf().AsImplementedInterfaces();
            builder.RegisterComponent(_arenaSpace).As<IArenaSpace>();
            builder.RegisterEntryPoint<InputRouter>().AsSelf();
            builder.RegisterComponent(_arenaView);
            builder.RegisterBuildCallback(resolver => Game.UI.MatchInterface.Create(
                gameObject, resolver.Resolve<HeroConfig>(), resolver.Resolve<HookConfig>(), resolver.Resolve<EconomyConfig>(),
                () => SceneUiNavigation.Navigate(this), () => _offlineDriver.ResetSandbox(),
                () => TrainingStatus()));
        }

        private string TrainingStatus()
        {
            var simulation = _offlineDriver.Simulation;
            if (simulation == null) return string.Empty;
            var hero = simulation.GetHero(0);
            var dummy = simulation.GetHero(1);
            var hook = simulation.GetHook(0);
            return $"Манекен: {dummy.Hp} HP · Хук: {hook.Phase} · Перезарядка: {System.Math.Max(0, hero.HookCooldownEnd - simulation.Time):0.0} с";
        }
    }
}