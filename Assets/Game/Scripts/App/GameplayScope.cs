using Game.Input.Gestures;
using VContainer;
using VContainer.Unity;

namespace Game.App
{
    public class GameplayScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Input. RegisterEntryPoint binds all implemented interfaces, including ITouchGestures.
            builder.RegisterEntryPoint<EnhancedTouchGestures>();
            builder.RegisterEntryPoint<TouchGesturesDebugLogger>();
        }
    }
}
