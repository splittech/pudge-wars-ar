using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Geometry;
using Game.Input.Gestures;
using R3;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TrackingState = UnityEngine.XR.ARSubsystems.TrackingState;

namespace Game.AR
{
    public sealed class RoomArenaAnchorProvider : IArenaAnchorProvider
    {
        private readonly ARSession _session;
        private readonly ARPlaneManager _planes;
        private readonly ARRaycastManager _raycasts;
        private readonly ARAnchorManager _anchors;
        private readonly Camera _camera;
        private readonly Transform _root;
        private readonly ITouchGestures _gestures;
        private readonly bool _host;
        private readonly bool _useAR;
        private readonly TrackingMonitor _tracking = new();
        private readonly ReactiveProperty<bool> _flatTracking = new(true);
        private readonly List<ARRaycastHit> _hits = new();
        private readonly List<float2> _boundary = new();
        private readonly List<float2[]> _obstacles = new();
        private readonly List<float2> _manual = new();
        private readonly List<IReadOnlyList<float2>> _preview = new();
        private readonly HashSet<TrackableId> _selectedPlanes = new();
        private readonly List<RoomScanSurface> _scanSurfaces = new();
        private RoomArenaOutline _outline;
        private ARAnchor _anchor;
        private Vector2? _tap;
        private Vector3 _origin;
        private bool _confirm;
        private bool _done;
        private bool _disposed;
        private bool _manualMode;
        private int _stage;
        private string _error;
        private bool _automaticScan;
        private double _nextScanTime;
        private int _skippedFurniture;

        public RoomArenaAnchorProvider(
            ARSession session,
            ARPlaneManager planes,
            ARRaycastManager raycasts,
            ARAnchorManager anchors,
            Camera camera,
            Transform root,
            ITouchGestures gestures,
            bool host,
            bool useAR)
        {
            _session = session;
            _planes = planes;
            _raycasts = raycasts;
            _anchors = anchors;
            _camera = camera;
            _root = root;
            _gestures = gestures;
            _host = host;
            _useAR = useAR;
            if (!useAR)
            {
                _stage = 2;
            }
        }

        public RoomArena Room { get; private set; }
        public RoomArena ReceivedRoom { get; set; }
        public string Message { get; private set; }
        public bool Unavailable { get; private set; }
        public bool IsAutomaticScan => _automaticScan;
        public bool CanEdit => _host && _stage >= 2 && !_done;
        public bool CanConfirm => !_done && IsTracking.CurrentValue && _stage >= 2
            && (_host ? _boundary.Count >= 3 && (!_manualMode || _manual.Count >= 3) : ReceivedRoom != null);
        public ReadOnlyReactiveProperty<bool> IsTracking => _useAR ? _tracking.IsTracking : _flatTracking;

        public void RefreshTracking()
        {
            if (_useAR)
            {
                _tracking.Refresh(_anchor);
            }
        }

        public void Confirm()
        {
            if (CanConfirm)
            {
                _confirm = true;
            }
        }

        public void BeginObstacle()
        {
            if (CanEdit && _stage == 3 && !_manualMode && _obstacles.Count < RoomArena.MaximumObstacles)
            {
                _manualMode = true;
                _error = null;
            }
        }

        public void BeginAutomaticScan()
        {
            if (!_host || _stage != 2 || _done)
            {
                return;
            }

            _automaticScan = true;
            _nextScanTime = 0;
            _boundary.Clear();
            _obstacles.Clear();
            _selectedPlanes.Clear();
            _error = null;
        }

        public void UpdateScanProposal(IReadOnlyList<RoomScanSurface> surfaces)
        {
            if (!_automaticScan || _done)
            {
                return;
            }

            _boundary.Clear();
            _obstacles.Clear();
            _selectedPlanes.Clear();
            if (!RoomScanProposal.TryCreate(
                surfaces,
                out RoomArena proposal,
                out IReadOnlyList<TrackableId> selected,
                out _skippedFurniture))
            {
                return;
            }

            _boundary.AddRange(proposal.GetPolygon(0));
            for (int i = 1; i < proposal.PolygonCount; i++)
            {
                IReadOnlyList<float2> polygon = proposal.GetPolygon(i);
                var points = new float2[polygon.Count];
                for (int j = 0; j < polygon.Count; j++)
                {
                    points[j] = polygon[j];
                }

                _obstacles.Add(points);
            }

            foreach (TrackableId id in selected)
            {
                _selectedPlanes.Add(id);
            }
        }

        public void Undo()
        {
            _automaticScan = false;
            _error = null;
            if (_manualMode)
            {
                if (_manual.Count > 0)
                {
                    _manual.RemoveAt(_manual.Count - 1);
                }
                else
                {
                    _manualMode = false;
                }
            }
            else if (_obstacles.Count > 0)
            {
                _obstacles.RemoveAt(_obstacles.Count - 1);
                _selectedPlanes.Clear();
            }
            else if (_boundary.Count > 0)
            {
                _stage = 2;
                _boundary.RemoveAt(_boundary.Count - 1);
            }
        }

        public async UniTask<Pose> AcquireArenaPoseAsync(CancellationToken cancellationToken)
        {
            Message = "Проверяем AR для сканирования комнаты…";
            if (_useAR)
            {
                if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                {
                    await ARSession.CheckAvailability().ToUniTask();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ARSession.state == ARSessionState.NeedsInstall)
                {
                    await ARSession.Install().ToUniTask();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
                {
                    Unavailable = true;
                    Message = "AR недоступен. Для проверки карты выберите плоский режим.";
                    await UniTask.WaitUntil(() => false, cancellationToken: cancellationToken);
                }

                _session.enabled = true;
                _planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                _planes.enabled = true;
                SetPlanesVisible(true);
                _root.localScale = Vector3.one * RoomArena.MetersPerUnit;
            }
            else
            {
                _stage = 2;
            }

            _outline = new RoomArenaOutline();
            using (IDisposable subscription = _gestures.Tap.Subscribe(point => _tap = point))
            {
                while (!_done)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RefreshTracking();
                    if (_automaticScan && _useAR && IsTracking.CurrentValue
                        && Time.realtimeSinceStartupAsDouble >= _nextScanTime)
                    {
                        _nextScanTime = Time.realtimeSinceStartupAsDouble + 1;
                        CaptureScanSurfaces();
                        UpdateScanProposal(_scanSurfaces);
                    }

                    if (_tap.HasValue && IsTracking.CurrentValue)
                    {
                        await HandleTapAsync(_tap.Value, cancellationToken);
                    }

                    _tap = null;
                    if (_confirm && CanConfirm)
                    {
                        ConfirmStage();
                    }

                    _confirm = false;
                    DrawPreview();
                    UpdateMessage();
                    await UniTask.Yield(cancellationToken);
                }
            }

            if (_useAR)
            {
                SetPlanesVisible(false);
                _planes.enabled = false;
            }

            return new Pose(_root.position, _root.rotation);
        }

        private async UniTask HandleTapAsync(Vector2 screen, CancellationToken cancellationToken)
        {
            _error = null;
            ARPlane plane = null;
            Vector3 world;
            if (_useAR)
            {
                if (!_raycasts.Raycast(screen, _hits, TrackableType.PlaneWithinPolygon))
                {
                    _error = "Поверхность ещё не найдена. Медленно осмотрите её камерой.";
                    return;
                }

                ARRaycastHit hit = _hits[0];
                plane = _planes.GetPlane(hit.trackableId);
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp)
                {
                    return;
                }

                world = hit.pose.position;
            }
            else
            {
                var floor = new Plane(_root.up, _root.position);
                Ray ray = _camera.ScreenPointToRay(screen);
                if (!floor.Raycast(ray, out float distance))
                {
                    return;
                }

                world = ray.GetPoint(distance);
            }

            if (_stage == 0)
            {
                _origin = world;
                _stage = 1;
                return;
            }

            if (_stage == 1)
            {
                Vector3 axis = world - _origin;
                axis.y = 0;
                if (axis.magnitude < 0.3f || Mathf.Abs(world.y - _origin.y) > 0.15f)
                {
                    _error = "Вторая точка должна быть на том же полу, не ближе 30 см.";
                    return;
                }

                Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(axis.normalized, Vector3.up), Vector3.up);
                var result = await _anchors.TryAddAnchorAsync(new Pose(_origin, rotation));
                if (_disposed || cancellationToken.IsCancellationRequested)
                {
                    if (result.status.IsSuccess() && result.value != null)
                    {
                        _anchors.TryRemoveAnchor(result.value);
                    }

                    throw new OperationCanceledException(cancellationToken);
                }

                if (!result.status.IsSuccess())
                {
                    _error = "Не удалось создать якорь. Повторите вторую точку.";
                    return;
                }

                _anchor = result.value;
                _root.SetParent(_anchor.transform, false);
                _root.localPosition = Vector3.zero;
                _root.localRotation = Quaternion.identity;
                _root.localScale = Vector3.one * RoomArena.MetersPerUnit;
                _stage = 2;
                if (_host)
                {
                    BeginAutomaticScan();
                }

                return;
            }

            if (!_host)
            {
                return;
            }

            Vector3 local = _root.InverseTransformPoint(world);
            if (_automaticScan)
            {
                if (_useAR && Mathf.Abs(local.y) > 1)
                {
                    _error = "Осмотрите комнату. Для ручной границы коснитесь пола, для проверки карты нажмите «Готово».";
                    return;
                }

                _automaticScan = false;
                _boundary.Clear();
                _obstacles.Clear();
                _selectedPlanes.Clear();
            }

            if (_stage == 3 && !_manualMode && plane != null)
            {
                if (local.y < 1 || _selectedPlanes.Contains(plane.trackableId))
                {
                    _error = "Выберите поверхность мебели выше пола или задайте контур вручную.";
                    return;
                }

                var points = new List<float2>();
                int count = plane.boundary.Length;
                if (count > RoomArena.MaximumVertices)
                {
                    _error = "Слишком сложная поверхность. Задайте контур мебели вручную.";
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    Vector2 point = plane.boundary[i];
                    Vector3 projected = _root.InverseTransformPoint(plane.transform.TransformPoint(new Vector3(point.x, 0, point.y)));
                    points.Add(new float2(projected.x, projected.z));
                }

                if (TryAddObstacle(points.ToArray()))
                {
                    _selectedPlanes.Add(plane.trackableId);
                }
            }
            else
            {
                if (_useAR && Mathf.Abs(local.y) > 1.5f)
                {
                    _error = "Углы контура отмечаются на полу.";
                    return;
                }

                List<float2> points = _manualMode ? _manual : _boundary;
                if (points.Count < RoomArena.MaximumVertices)
                {
                    points.Add(new float2(local.x, local.z));
                }
            }
        }

        private bool TryAddObstacle(float2[] points)
        {
            if (_obstacles.Count >= RoomArena.MaximumObstacles)
            {
                _error = "Достигнут предел 16 препятствий.";
                return false;
            }

            _obstacles.Add(points);
            try
            {
                _ = new RoomArena(_boundary.ToArray(), _obstacles.ToArray());
                return true;
            }
            catch (ArgumentException)
            {
                _obstacles.RemoveAt(_obstacles.Count - 1);
                _error = "Контур мебели пересекается, выходит за комнату или слишком мал. Исправьте точки.";
                return false;
            }
        }

        private void ConfirmStage()
        {
            if (!_host)
            {
                Room = ReceivedRoom;
                _done = true;
                return;
            }

            try
            {
                var room = new RoomArena(_boundary.ToArray(), _obstacles.ToArray());
                if (_stage == 2)
                {
                    _automaticScan = false;
                    _stage = 3;
                }
                else if (_manualMode)
                {
                    if (TryAddObstacle(_manual.ToArray()))
                    {
                        _manual.Clear();
                        _manualMode = false;
                    }
                }
                else
                {
                    Room = room;
                    _done = true;
                }
            }
            catch (ArgumentException)
            {
                _error = "Контур комнаты пересекается или слишком мал. Отмените и исправьте последние точки.";
            }
        }

        private void UpdateMessage()
        {
            string instruction;
            if (_stage == 0)
            {
                instruction = "Осмотрите пол. Коснитесь первой общей точки — начала координат.";
            }
            else if (_stage == 1)
            {
                instruction = "Коснитесь второй общей точки на полу — направления +X. Все игроки выбирают те же точки.";
            }
            else if (!_host)
            {
                instruction = ReceivedRoom == null ? "Ждём карту комнаты от хоста…" : "Проверьте совпадение контуров с комнатой и мебелью. Нажмите «Готово».";
            }
            else if (_stage == 2)
            {
                if (_automaticScan)
                {
                    instruction = "Медленно осмотрите пол и мебель. Контуры предлагаются автоматически. "
                        + $"Найдено препятствий: {_obstacles.Count}, пропущено поверхностей: {_skippedFurniture}. "
                        + "«Готово» — остановить сканирование и проверить карту. Тап по полу — ручная граница. "
                        + "Найденный участок пола может быть неполным.";
                    if (_boundary.Count == 0)
                    {
                        instruction = "Нужен найденный участок пола площадью от 1 м² вокруг общей точки. " + instruction;
                    }
                }
                else
                {
                    instruction = $"Отметьте по порядку углы комнаты на полу ({_boundary.Count}/64). «Готово» — перейти к мебели.";
                }
            }
            else if (_manualMode)
            {
                instruction = $"Отметьте контур мебели на полу ({_manual.Count}/64). «Готово» — добавить препятствие.";
            }
            else
            {
                instruction = $"Осмотрите мебель и коснитесь её найденных поверхностей. Препятствий: {_obstacles.Count}/16. «Готово» — принять карту.";
            }

            Message = string.IsNullOrEmpty(_error) ? instruction : _error + "\n" + instruction;
        }

        private void DrawPreview()
        {
            _preview.Clear();
            if (!_host && ReceivedRoom != null)
            {
                for (int i = 0; i < ReceivedRoom.PolygonCount; i++)
                {
                    _preview.Add(ReceivedRoom.GetPolygon(i));
                }
            }
            else
            {
                _preview.Add(_boundary);
                foreach (float2[] polygon in _obstacles)
                {
                    _preview.Add(polygon);
                }

                if (_manualMode)
                {
                    _preview.Add(_manual);
                }
            }

            _outline.Draw(_root, _preview, !_done && _stage >= 2);
        }

        private void SetPlanesVisible(bool visible)
        {
            foreach (ARPlane plane in _planes.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }

        private void CaptureScanSurfaces()
        {
            _scanSurfaces.Clear();
            foreach (ARPlane plane in _planes.trackables)
            {
                if (plane.alignment != PlaneAlignment.HorizontalUp || plane.subsumedBy != null
                    || plane.trackingState == TrackingState.None)
                {
                    continue;
                }

                int count = plane.boundary.Length;
                float2[] points = Array.Empty<float2>();
                if (count >= 3 && count <= RoomArena.MaximumVertices)
                {
                    points = new float2[count];
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 point = plane.boundary[i];
                        Vector3 world = plane.transform.TransformPoint(new Vector3(point.x, 0, point.y));
                        Vector3 local = _root.InverseTransformPoint(world);
                        points[i] = new float2(local.x, local.z);
                    }
                }

                float height = _root.InverseTransformPoint(plane.transform.position).y * RoomArena.MetersPerUnit;
                _scanSurfaces.Add(new RoomScanSurface(plane.trackableId, height, points));
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _outline?.Dispose();
            if (_root != null && _anchor != null)
            {
                _root.SetParent(null, true);
            }

            if (_anchor != null && _anchors != null && _anchors.enabled)
            {
                _anchors.TryRemoveAnchor(_anchor);
            }

            _tracking.Dispose();
            _flatTracking.Dispose();
        }
    }
}
