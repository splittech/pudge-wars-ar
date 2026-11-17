using VContainer.Unity;
using VContainer;
using Game.UI;
namespace Game.App
{
    public class MainMenuScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<NetworkMenuView>();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<NetworkMenuView>()
                .SetTrainingAction(() => SceneUiNavigation.Navigate(this)));
            builder.RegisterBuildCallback(resolver => resolver.Resolve<NetworkMenuView>()
                .SetArenaModeActions(() => Game.Input.Routing.ArenaMode.UseAR = !Game.Input.Routing.ArenaMode.UseAR,
                    () => Game.Input.Routing.ArenaMode.UseAR));
        }
    }
}
