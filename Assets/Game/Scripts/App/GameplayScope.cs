using Game.Input.Gestures;
using Game.Input.Routing;
using Game.Presentation;
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

        protected override void Configure(IContainerBuilder builder)
        {
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
