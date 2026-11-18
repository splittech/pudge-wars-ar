using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Configs;
using Game.Input.Gestures;
using R3;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Game.AR
{
    public sealed class ARArenaAnchorProvider : IArenaAnchorProvider
    {
        private readonly ARSession _session;
        private readonly ARPlaneManager _planes;
        private readonly ARRaycastManager _raycasts;
        private readonly ARAnchorManager _anchors;
        private readonly Camera _camera;
        private readonly Transform _root;
        private readonly ITouchGestures _gestures;
        private readonly InputConfigData _config;
        private readonly Vector2 _size;
        private readonly TrackingMonitor _tracking = new();
        private readonly List<ARRaycastHit> _hits = new();
        private ARAnchor _anchor;
        private LineRenderer _preview;
        private Material _material;
        private bool _tap;
        private bool _confirm;
        private bool _disposed;
        private float _yaw;

        public ARArenaAnchorProvider(
            ARSession session,
            ARPlaneManager planes,
            ARRaycastManager raycasts,
            ARAnchorManager anchors,
            Camera camera,
            Transform root,
            ITouchGestures gestures,
            InputConfigData config,
            Vector2 size)
        {
            _session = session;
            _planes = planes;
            _raycasts = raycasts;
            _anchors = anchors;
            _camera = camera;
            _root = root;
            _gestures = gestures;
            _config = config;
            _size = size;
        }

        public string Message { get; private set; }
        public bool Unavailable { get; private set; }
        public bool CanConfirm => !_disposed && !_confirm && _anchor != null && IsTracking.CurrentValue;
        public ReadOnlyReactiveProperty<bool> IsTracking => _tracking.IsTracking;

        public void Confirm()
        {
            if (CanConfirm)
            {
                _confirm = true;
            }
        }

        public void RefreshTracking()
        {
            _tracking.Refresh(_anchor);
        }

        public async UniTask<Pose> AcquireArenaPoseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Message = "Проверяем поддержку AR…";
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
            {
                // Availability changes global ARSession state; let it finish even if this flow is cancelled.
                await ARSession.CheckAvailability().ToUniTask();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                Message = "Установите сервисы Google Play для AR";
                await ARSession.Install().ToUniTask();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
            {
                Unavailable = true;
                Message = "AR недоступен. Выберите плоский режим.";
                await UniTask.WaitUntil(() => false, cancellationToken: cancellationToken);
            }

            _session.enabled = true;
            _planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            _planes.enabled = true;
            SetPlanesVisible(true);
            _root.localScale = Vector3.one * Mathf.Clamp(0.04f, _config.ArenaScaleMin, _config.ArenaScaleMax);
            CreatePreview();
            IDisposable tap = _gestures.Tap.Subscribe(_ => _tap = true);
            IDisposable pinch = _gestures.PinchTwist.Subscribe(OnPinchTwist);
            try
            {
                Message = "Медленно двигайте телефон: найдите горизонтальную поверхность";
                await UniTask.WaitUntil(() => _planes.trackables.count > 0, cancellationToken: cancellationToken);
                while (!_confirm)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RefreshTracking();
                    bool hit = _anchor != null || _raycasts.Raycast(
                        _camera.pixelRect.center,
                        _hits,
                        TrackableType.PlaneWithinPolygon);
                    _preview.enabled = hit && IsTracking.CurrentValue;
                    if (_anchor == null && hit)
                    {
                        Pose pose = _hits[0].pose;
                        _root.SetPositionAndRotation(pose.position, pose.rotation);
                    }

                    DrawPreview();
                    if (_anchor != null)
                    {
                        Message = "Двумя пальцами измените масштаб и поворот. Нажмите «Готово».";
                    }
                    else if (hit)
                    {
                        Message = "Наведите контур на стол и коснитесь экрана";
                    }
                    else
                    {
                        Message = "Наведите центр экрана на найденную поверхность";
                    }

                    if (_tap && _anchor == null && hit && IsTracking.CurrentValue && _anchors.subsystem != null)
                    {
                        _tap = false;
                        var result = await _anchors.TryAddAnchorAsync(new Pose(_root.position, _root.rotation));
                        if (_disposed || cancellationToken.IsCancellationRequested)
                        {
                            if (result.status.IsSuccess() && result.value != null)
                            {
                                _anchors.TryRemoveAnchor(result.value);
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            throw new OperationCanceledException();
                        }

                        if (result.status.IsSuccess())
                        {
                            _anchor = result.value;
                            _root.SetParent(_anchor.transform, true);
                        }
                        else
                        {
                            Message = "Не удалось создать якорь. Попробуйте ещё раз.";
                        }
                    }

                    _tap = false;
                    await UniTask.Yield(cancellationToken);
                }

                SetPlanesVisible(false);
                _planes.enabled = false;
                return new Pose(_root.position, _root.rotation);
            }
            finally
            {
                tap.Dispose();
                pinch.Dispose();
                if (_preview != null)
                {
                    _preview.enabled = false;
                }
            }
        }

        private void OnPinchTwist(PinchTwistGesture gesture)
        {
            if (!CanConfirm || !float.IsFinite(gesture.ScaleFactor) || gesture.ScaleFactor <= 0f
                || !float.IsFinite(gesture.AngleDelta))
            {
                return;
            }

            float scale = Mathf.Clamp(
                _root.localScale.x * gesture.ScaleFactor,
                _config.ArenaScaleMin,
                _config.ArenaScaleMax);
            _root.localScale = Vector3.one * scale;
            _yaw += gesture.AngleDelta;
            _root.localRotation = Quaternion.AngleAxis(_yaw, Vector3.up);
        }

        private void SetPlanesVisible(bool visible)
        {
            foreach (var plane in _planes.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }

        private void CreatePreview()
        {
            var go = new GameObject("Arena placement preview");
            _preview = go.AddComponent<LineRenderer>();
            _preview.positionCount = 4;
            _preview.loop = true;
            _preview.useWorldSpace = true;
            _preview.startWidth = 0.003f;
            _preview.endWidth = 0.003f;
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _material.color = new Color(0.2f, 0.85f, 1f, 0.65f);
            _material.SetFloat("_Surface", 1);
            _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_ZWrite", 0);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.renderQueue = 3000;
            _preview.sharedMaterial = _material;
        }

        private void DrawPreview()
        {
            for (int i = 0; i < 4; i++)
            {
                float xSign = i < 2 ? -1f : 1f;
                float zSign = i == 0 || i == 3 ? -1f : 1f;
                var corner = new Vector3(xSign * _size.x / 2f, 0.02f, zSign * _size.y / 2f);
                _preview.SetPosition(i, _root.TransformPoint(corner));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _tracking.Dispose();
            if (_root != null && _anchor != null)
            {
                _root.SetParent(null, true);
            }

            if (_anchor != null && _anchors != null && _anchors.enabled)
            {
                _anchors.TryRemoveAnchor(_anchor);
            }

            if (_preview != null)
            {
                UnityEngine.Object.Destroy(_preview.gameObject);
            }

            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
            }

            if (_planes != null)
            {
                _planes.enabled = false;
            }

            if (_session != null)
            {
                _session.enabled = false;
            }
        }
    }
}
