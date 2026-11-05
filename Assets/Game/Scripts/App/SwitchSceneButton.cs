using Cysharp.Threading.Tasks;
using Game.Services.LoadingScreen;
using Game.Services.SceneService;
using Game.Net;
using Mirror;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.App
{
    public class SwitchSceneButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private SceneMetaAsset _sceneToSwitch;
        [SerializeField] private string _switchSceneProgressText;

        private ISceneService _sceneService;
        private IGameLoadingScreen _loadingScreen;
        private PudgeNetworkManager _networkManager;
        private INetworkSceneLoader _networkScenes;

        [Inject]
        public void Construct(IGameLoadingScreen loadingScreen, ISceneService sceneService,
            PudgeNetworkManager networkManager, INetworkSceneLoader networkScenes)
        {
            _sceneService = sceneService;
            _loadingScreen = loadingScreen;
            _networkManager = networkManager;
            _networkScenes = networkScenes;
        }

        private void Start()
        {
            _button.OnClickAsObservable()
                .Subscribe(_ => SwitchSceneAsync().Forget())
                .AddTo(this);
        }

        private async UniTask SwitchSceneAsync()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                if (_sceneToSwitch.SceneReference.Path == _networkScenes.OfflineScenePath)
                    _networkManager.StopSession();
                return;
            }
            _button.interactable = false;
            try
            {
                await _loadingScreen.ShowScreenAsync();
                _loadingScreen.SetProgressDescription(_switchSceneProgressText);
                await _sceneService.SwitchSceneAsync(_sceneToSwitch, _loadingScreen.LoadingProgress);
            }
            finally
            {
                await _loadingScreen.HideScreenAsync();
                if (this != null)
                    _button.interactable = true;
            }
        }
    }
}
