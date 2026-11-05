using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Net;
using Game.Services.LoadingScreen;
using Game.Services.SceneService;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.App
{
    public sealed class NetworkSceneLoader : MonoBehaviour, INetworkSceneLoader
    {
        [SerializeField] private SceneMetaAsset _menuScene;
        [SerializeField] private SceneMetaAsset _matchScene;

        private ISceneService _sceneService;
        private IGameLoadingScreen _loadingScreen;
        private ProjectBootstrap _bootstrap;

        public bool IsReadyToStart => _bootstrap != null && _bootstrap.IsInitialized;
        public string OfflineScenePath => _menuScene.SceneReference.Path;
        public string OnlineScenePath => _matchScene.SceneReference.Path;

        [Inject]
        public void Construct(ISceneService sceneService, IGameLoadingScreen loadingScreen, ProjectBootstrap bootstrap)
        {
            _sceneService = sceneService;
            _loadingScreen = loadingScreen;
            _bootstrap = bootstrap;
        }

        public async UniTask LoadAsync(string scenePath, CancellationToken ct)
        {
            SceneMetaAsset target = scenePath == OnlineScenePath ? _matchScene
                : scenePath == OfflineScenePath ? _menuScene
                : throw new ArgumentException("Scene is not configured for network transitions.", nameof(scenePath));

            try
            {
                await _loadingScreen.ShowScreenAsync(ct);
                _loadingScreen.SetProgressDescription(target == _matchScene ? "Joining game" : "Returning to menu");
                await _sceneService.SwitchSceneAsync(target, _loadingScreen.LoadingProgress, ct);
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                    throw new InvalidOperationException($"Network scene was not loaded: {scenePath}");
                SceneManager.SetActiveScene(scene);
            }
            finally
            {
                // A failed load must not leave an opaque overlay covering the application.
                if (this != null)
                    _loadingScreen.HideScreenImmediate();
            }
        }
    }
}
