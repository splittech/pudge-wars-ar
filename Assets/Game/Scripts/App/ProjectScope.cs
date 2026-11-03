using Game.Core.Configs;
using Game.Services.LoggingService;
using Game.Services.LoadingScreen;
using Game.Services.SceneService;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.App
{
    public class ProjectScope : LifetimeScope
    {
        [Header("Project Scope")]
        [SerializeField] private ProjectBootstrap _projectBootstrap;
        [SerializeField] private GameLoadingScreen _loadingScreen;

        [Header("Configs")]
        [SerializeField] private SceneServiceConfig _sceneServiceConfig;
        [SerializeField] private LoggingServiceConfig _loggingServiceConfig;
        [SerializeField] private InputConfig _inputConfig;
        [SerializeField] private HookConfig _hookConfig;
        [SerializeField] private HeroConfig _heroConfig;
        [SerializeField] private EconomyConfig _economyConfig;
        [SerializeField] private ArenaConfig _arenaConfig;
        [SerializeField] private MatchConfig _matchConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            // Bootstrap
            builder.RegisterComponent(_projectBootstrap);

            // Loading Screen
            builder.RegisterComponent(_loadingScreen).As<IGameLoadingScreen>();

            // Scene Service
            builder.Register<SceneService>(Lifetime.Singleton).As<ISceneService>();
            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.RegisterInstance(_sceneServiceConfig);

            // Logging Service
            builder.Register<LoggingService>(Lifetime.Singleton).As<ILoggingService>();
            builder.RegisterInstance(_loggingServiceConfig);

            // Game Configs
            builder.RegisterInstance(_inputConfig);
            builder.RegisterInstance(_hookConfig);
            builder.RegisterInstance(_heroConfig);
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_arenaConfig);
            builder.RegisterInstance(_matchConfig);
        }
    }
}
