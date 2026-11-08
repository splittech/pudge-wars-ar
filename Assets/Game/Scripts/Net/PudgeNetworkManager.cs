using System;
using Alchemy.Inspector;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;
using VContainer;

namespace Game.Net
{
    [DisallowMultipleComponent]
    public sealed class PudgeNetworkManager : NetworkManager
    {
        private INetworkSceneLoader _sceneLoader;
        private string _pendingScene;
        private string _currentScene;
        private int _transitionVersion;
        private bool _quitting;
        private NetworkPrefabFactory _matchFactory;
        private bool _playerRequested;

        public bool IsChangingScene { get; private set; }

        public override void Awake()
        {
            // Boot owns the lifetime. Mirror must never move this object into Gameplay.
            dontDestroyOnLoad = false;
            offlineScene = string.Empty;
            autoCreatePlayer = false;
            playerPrefab = null;
            spawnPrefabs.Clear();
            sendRate = 30;
            maxConnections = 4;
            base.Awake();
        }

        [Inject]
        public void Construct(INetworkSceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
            onlineScene = sceneLoader.OnlineScenePath;
        }

        [BoxGroup("Session", -10)]
        [HorizontalGroup("Session/Start")]
        [Button, LabelText("Start Host"), EnableIf(nameof(CanStartSession))]
        public void StartHostSession()
        {
            if (CanStartSession())
                StartHost();
        }

        [BoxGroup("Session", -10)]
        [HorizontalGroup("Session/Start")]
        [Button, LabelText("Start Client"), EnableIf(nameof(CanStartSession))]
        public void StartClientSession()
        {
            if (CanStartSession())
                StartClient();
        }

        [BoxGroup("Session", -10)]
        [Button, LabelText("Stop Session"), EnableIf(nameof(CanStopSession))]
        public void StopSession()
        {
            // During StartHost's scene load, ConnectHost has not created the local client yet.
            if (mode == NetworkManagerMode.Host && NetworkClient.active)
                StopHost();
            else if (NetworkServer.active)
                StopServer();
            else if (NetworkClient.active)
                StopClient();
        }

        private bool CanStartSession()
        {
            return Application.isPlaying && _sceneLoader != null && !IsChangingScene
                && _sceneLoader.IsReadyToStart
                && !NetworkServer.active && !NetworkClient.active;
        }

        private bool CanStopSession()
        {
            return Application.isPlaying && (NetworkServer.active || NetworkClient.active);
        }

        public override void ServerChangeScene(string newSceneName)
        {
            if (_sceneLoader == null || newSceneName != _sceneLoader.OnlineScenePath)
                throw new ArgumentException("Only the configured online scene is allowed.", nameof(newSceneName));
            if (!NetworkServer.active)
                throw new InvalidOperationException("An active server is required to change the network scene.");

            NetworkServer.SetAllClientsNotReady();
            networkSceneName = newSceneName;
            NetworkServer.isLoadingScene = true;
            OnServerChangeScene(newSceneName);
            NetworkServer.SendToAll(new SceneMessage { sceneName = newSceneName, customHandling = true });
            startPositionIndex = 0;
            startPositions.Clear();
            RequestScene(newSceneName);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Also handles the normal SceneMessage sent to late joiners during authentication.
            NetworkClient.ReplaceHandler<SceneMessage>(OnSceneMessage);
        }

        internal void AttachMatchFactory(NetworkPrefabFactory factory)
        {
            if (_matchFactory != null && _matchFactory != factory)
                throw new InvalidOperationException("A network match factory is already attached.");
            _matchFactory = factory;
        }

        internal void DetachMatchFactory(NetworkPrefabFactory factory)
        {
            if (_matchFactory == factory)
                _matchFactory = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // In host mode Gameplay is loaded before OnStartServer; dedicated servers load afterwards.
            _matchFactory?.EnsureMatchState();
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            _matchFactory?.EnsureMatchState();
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            TryRequestPlayer();
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            TryRequestPlayer();
        }

        private void TryRequestPlayer()
        {
            if (_matchFactory != null && NetworkClient.ready && !_playerRequested && NetworkClient.localPlayer == null)
                _playerRequested = NetworkClient.AddPlayer();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient connection)
        {
            try
            {
                if (_matchFactory == null)
                    throw new InvalidOperationException("Gameplay DI scope is not ready.");
                _matchFactory.SpawnPlayer(connection);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Player spawn rejected: {exception.Message}");
                connection.Disconnect();
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            _matchFactory?.ReleasePlayer(connection);
            base.OnServerDisconnect(connection);
        }

        private void OnSceneMessage(SceneMessage message)
        {
            if (NetworkServer.active)
                return;
            if (_sceneLoader == null || message.sceneName != _sceneLoader.OnlineScenePath
                || message.sceneOperation != SceneOperation.Normal)
            {
                Debug.LogError("Server requested an unsupported network scene.");
                StopSession();
                return;
            }

            networkSceneName = message.sceneName;
            NetworkClient.isLoadingScene = true;
            RequestScene(message.sceneName);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RequestOfflineScene();
        }

        public override void OnStopClient()
        {
            _playerRequested = false;
            base.OnStopClient();
            // StopHost calls StopClient before StopServer; the server requests the return.
            if (!NetworkServer.active)
                RequestOfflineScene();
        }

        private void RequestOfflineScene()
        {
            if (!_quitting && _sceneLoader != null)
                RequestScene(_sceneLoader.OfflineScenePath);
        }

        private void RequestScene(string scenePath)
        {
            if (_pendingScene == scenePath
                || (IsChangingScene && _pendingScene == null && _currentScene == scenePath))
                return;
            _pendingScene = scenePath;
            _transitionVersion++;
            if (!IsChangingScene)
                ProcessScenesAsync().Forget();
        }

        private async UniTask ProcessScenesAsync()
        {
            IsChangingScene = true;
            try
            {
                // Mirror must finish its synchronous start/stop callbacks before we complete a load.
                await UniTask.Yield();
                while (_pendingScene != null)
                {
                    string scenePath = _pendingScene;
                    _currentScene = scenePath;
                    int version = _transitionVersion;
                    _pendingScene = null;
                    await _sceneLoader.LoadAsync(scenePath, destroyCancellationToken);
                    if (version != _transitionVersion || _quitting)
                        continue;

                    if (scenePath == _sceneLoader.OnlineScenePath
                        && (NetworkServer.active || NetworkClient.active))
                    {
                        if (_matchFactory == null)
                            throw new InvalidOperationException("Network scene loaded without its DI spawn factory.");
                        FinishLoadScene();
                    }
                    else
                    {
                        NetworkServer.isLoadingScene = false;
                        NetworkClient.isLoadingScene = false;
                    }
                }
            }
            catch (OperationCanceledException) when (this == null || _quitting)
            {
                // Editor shutdown or application exit destroys Boot.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _pendingScene = null;
                StopSession();
                _pendingScene = null;
                // Recover once after an online load failure; never retry a failed menu forever.
                if (!_quitting && _currentScene != _sceneLoader.OfflineScenePath)
                {
                    try
                    {
                        await _sceneLoader.LoadAsync(_sceneLoader.OfflineScenePath, destroyCancellationToken);
                    }
                    catch (Exception recoveryException)
                    {
                        Debug.LogException(recoveryException);
                    }
                }
            }
            finally
            {
                IsChangingScene = false;
                _currentScene = null;
                NetworkServer.isLoadingScene = false;
                NetworkClient.isLoadingScene = false;
            }
        }

        public override void OnApplicationQuit()
        {
            _quitting = true;
            base.OnApplicationQuit();
        }
    }
}
