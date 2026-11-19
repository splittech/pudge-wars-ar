using Game.Input.Routing;
using Game.Net;
using Game.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.App
{
    public class MainMenuScope : LifetimeScope
    {
        [SerializeField] private MenuDocument _menuDocument;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterBuildCallback(resolver =>
            {
                _menuDocument.ViewModel.Initialize(
                    resolver.Resolve<NetworkSessionController>(),
                    () => SceneUiNavigation.Navigate(this),
                    () => ArenaMode.UseAR = !ArenaMode.UseAR,
                    () => ArenaMode.UseAR,
                    QuitApplication);
            });
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
