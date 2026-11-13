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
        }
    }
}
