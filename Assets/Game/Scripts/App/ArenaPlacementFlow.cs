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
using Game.UI.Foundation;
using UnityEngine;

namespace Game.App
{
    public sealed class ArenaPlacementFlow : MonoBehaviour
    {
        [SerializeField] private ARArenaRig _rig;
        private Transform _root, _originalParent;
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
        private ReactiveDocument _ui;
        private bool _mode, _placed, _running, _sent;
        private MatchPhase _previousPhase;
        private HeroNet _previousHero;
        private Camera[] _otherCameras;
        private string _failure;
        private readonly List<Renderer> _renderers = new();

        public void Initialize(Transform root, FlatArenaSpace space, ITouchGestures gestures, InputRouter router,
            InputConfig input, ArenaConfig arena)
        {
            _root = root;
            _originalParent = root.parent;
            _flatPose = new Pose(root.position, root.rotation);
            _flatScale = root.localScale;
            _space = space;
            _flatCamera = space.ActiveCamera;
            _gestures = gestures;
            _router = router;
            _otherCameras = Array.FindAll(Camera.allCameras, camera => camera != _flatCamera && camera != _rig.Camera);
            foreach (var camera in _otherCameras)
            {
                camera.enabled = false;
            }

            _input = input.ToData();
            var size = arena.ToData().Size;
            _size = new Vector2(size.x, size.y);
            _ui = ToolkitRuntime.Create(gameObject, "Placement", "PlacementState", 50);
            _ui.ActionRequested += OnAction;
            _mode = ArenaMode.UseAR;
            _space.InputEnabled = false;
            UpdateVisibility();
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (_ui == null)
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
            _ui.Model.Visible("trackingLost", lost);
            _ui.Model.Visible("placementPanel", _running || (_placed && phase == MatchPhase.Placement));
            var provider = _provider as ARArenaAnchorProvider;
            string placementMessage;

            if (_placed)
            {
                placementMessage = "Арена установлена. Ждём остальных игроков…";
            }
            else
            {
                placementMessage = _failure ?? provider?.Message ?? "Подготовка арены…";
            }

            _ui.Model.Text("placementMessage", placementMessage);
            _ui.Model.Visible("confirmPlacement", _running && provider != null && !provider.Unavailable);
            _ui.Model.Enabled("confirmPlacement", provider?.CanConfirm ?? false);
            _ui.Model.Visible("useFlat", _running && _mode);
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
            _flatCamera.enabled = !_mode;
            var listener = _flatCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = !_mode;
            }

            _space.SetCamera(_mode ? _rig.Camera : _flatCamera);
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            var cancellation = _cancellation;
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
                Pose pose = await _provider.AcquireArenaPoseAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                _root.SetPositionAndRotation(pose.position, pose.rotation);
                _placed = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _failure = "Не удалось запустить AR. Выберите плоский режим.";
                // Keep the fallback available without restarting the failing operation every frame.
                await UniTask.WaitUntil(() => cancellation.IsCancellationRequested);
            }
            finally
            {
                if (_cancellation == cancellation)
                {
                    _running = false;
                }
            }
        }

        private void OnAction(string action, string value)
        {
            if (action == "confirmPlacement" && _provider is ARArenaAnchorProvider ar)
            {
                ar.Confirm();
            }

            if (action == "useFlat")
            {
                ArenaMode.UseAR = false;
            }
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

        private void LateUpdate() => UpdateVisibility();

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
            if (_ui != null)
            {
                _ui.ActionRequested -= OnAction;
            }

            StopProvider();
            if (_otherCameras != null)
            {
                foreach (var camera in _otherCameras)
                {
                    if (camera != null)
                    {
                        camera.enabled = true;
                    }
                }
            }
        }
    }
}
