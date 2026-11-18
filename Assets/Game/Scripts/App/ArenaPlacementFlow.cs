using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.AR;
using Game.Core.Configs;
using Game.Core.Match;
using Game.Input.Gestures;
using Game.Input.Routing;
using Game.Net;
using UnityEngine;

namespace Game.App
{
    public sealed class ArenaPlacementFlow : MonoBehaviour, IArenaPlacementService
    {
        [SerializeField] private ARArenaRig _rig;
        private Transform _root;
        private Transform _originalParent;
        private Pose _flatPose;
        private Vector3 _flatScale;
        private FlatArenaSpace _space;
        private Camera _flatCamera;
        private ITouchGestures _gestures;
        private InputRouter _router;
        private InputConfigData _input;
        private Vector2 _size;
        private IArenaAnchorProvider _provider;
        private CancellationTokenSource _cancellation;
        private bool _mode;
        private bool _placed;
        private bool _running;
        private bool _sent;
        private MatchPhase _previousPhase;
        private HeroNet _previousHero;
        private ArenaCameraSwitcher _cameras;
        private string _failure;
        private readonly List<Renderer> _renderers = new();

        public void Initialize(
            Transform root,
            FlatArenaSpace space,
            ITouchGestures gestures,
            InputRouter router,
            InputConfig input,
            ArenaConfig arena)
        {
            _root = root;
            _originalParent = root.parent;
            _flatPose = new Pose(root.position, root.rotation);
            _flatScale = root.localScale;
            _space = space;
            _flatCamera = space.ActiveCamera;
            _gestures = gestures;
            _router = router;
            // XR Simulation owns additional cameras outside the application's asset scenes.
            Camera[] backgroundCameras = Array.FindAll(
                Camera.allCameras,
                camera => camera.gameObject.scene.path.StartsWith("Assets/Game/", StringComparison.Ordinal));
            _cameras = new ArenaCameraSwitcher(_flatCamera, _rig.Camera, backgroundCameras);

            _input = input.ToData();
            var size = arena.ToData().Size;
            _size = new Vector2(size.x, size.y);
            _mode = ArenaMode.UseAR;
            _space.InputEnabled = false;
            UpdateVisibility();
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (_root == null)
            {
                return;
            }

            bool network = Mirror.NetworkClient.active || Mirror.NetworkServer.active;
            MatchPhase phase;

            if (MatchStateNet.Active != null)
            {
                phase = MatchStateNet.Active.Summary.Phase;
            }
            else if (network)
            {
                phase = MatchPhase.Lobby;
            }
            else
            {
                phase = MatchPhase.Playing;
            }
            if (ArenaMode.UseAR != _mode)
            {
                _mode = ArenaMode.UseAR;
                StopProvider();
                _placed = false;
            }
            if (phase != _previousPhase || _previousHero != HeroNet.Local)
            {
                _sent = false;
            }

            _previousPhase = phase;
            _previousHero = HeroNet.Local;
            bool placementAllowed = !network || (MatchStateNet.Active != null && phase != MatchPhase.Lobby);
            if (!_running && !_placed && placementAllowed)
            {
                PlaceAsync().Forget();
            }

            if (_provider is ARArenaAnchorProvider ar)
            {
                ar.RefreshTracking();
            }

            bool tracking = _provider?.IsTracking.CurrentValue ?? false;
            bool canInput = _placed && tracking && placementAllowed;
            if (!canInput)
            {
                _router.CancelAim();
            }

            _space.InputEnabled = canInput;
            bool lost = _placed && _mode && !tracking;
            var provider = _provider as ARArenaAnchorProvider;
            string message;

            if (_placed)
            {
                message = "Арена установлена. Ждём остальных игроков…";
            }
            else
            {
                message = _failure ?? provider?.Message ?? "Подготовка арены…";
            }

            var status = new ArenaPlacementStatus(
                message,
                _running || (_placed && phase == MatchPhase.Placement),
                _running && provider != null && !provider.Unavailable,
                provider?.CanConfirm ?? false,
                _running && _mode,
                lost);

            if (!Status.Equals(status))
            {
                Status = status;
                StatusChanged?.Invoke(status);
            }

            if (_placed && !_sent && phase == MatchPhase.Placement && HeroNet.Local != null)
            {
                HeroNet.Local.ArenaPlaced();
                _sent = true;
            }
        }

        private async UniTask PlaceAsync()
        {
            _running = true;
            _failure = null;
            _space.InputEnabled = false;
            if (_root != null)
            {
                _root.SetParent(_originalParent, true);
            }

            _root.SetPositionAndRotation(_flatPose.position, _flatPose.rotation);
            _root.localScale = _flatScale;
            _rig.SetActive(_mode);
            _cameras.SetMode(_mode);

            _space.SetCamera(_mode ? _rig.Camera : _flatCamera);
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationTokenSource cancellation = _cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            if (_mode)
            {
                _provider = _rig.CreateProvider(_root, _gestures, _input, _size);
            }
            else
            {
                _provider = new FlatArenaAnchorProvider(_flatPose);
            }
            try
            {
                Pose pose = await _provider.AcquireArenaPoseAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _root.SetPositionAndRotation(pose.position, pose.rotation);
                _placed = true;
            }
            catch (OperationCanceledException)
            {
                // Changing mode or destroying the scene cancels placement.
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested || _cancellation != cancellation)
                {
                    return;
                }

                Debug.LogException(exception);
                _failure = "Не удалось запустить AR. Выберите плоский режим.";
                // Keep the fallback available without restarting the failing operation every frame.
                await UniTask.WaitUntil(() => cancellationToken.IsCancellationRequested);
            }
            finally
            {
                if (_cancellation == cancellation)
                {
                    _running = false;
                }
            }
        }

        public ArenaPlacementStatus Status { get; private set; }

        public event Action<ArenaPlacementStatus> StatusChanged;

        public void Confirm()
        {
            if (_provider is ARArenaAnchorProvider provider)
            {
                provider.Confirm();
            }
        }

        public void UseFlat()
        {
            ArenaMode.UseAR = false;
        }

        private void StopProvider()
        {
            _cancellation?.Cancel();
            _provider?.Dispose();
            _provider = null;
            _cancellation?.Dispose();
            _cancellation = null;
            _running = false;
            if (_root != null)
            {
                _root.SetParent(_originalParent, true);
            }
        }

        private void LateUpdate()
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (_root == null)
            {
                return;
            }

            _root.GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
            {
                renderer.forceRenderingOff = !_placed;
            }

            foreach (var hero in HeroNet.ClientHeroes)
            {
                if (hero != null && hero.gameObject.scene == gameObject.scene)
                {
                    hero.IsArenaVisible = _placed;
                }
            }
        }

        private void OnDestroy()
        {
            StatusChanged = null;
            StopProvider();

            if (_rig != null)
            {
                _rig.SetActive(false);
            }

            _cameras?.Dispose();
        }
    }
}
