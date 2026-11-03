using UnityEngine;
using VContainer;

namespace Game.Services.RaycastService
{
    public class RaycastService : MonoBehaviour, IRaycastService
    {
        private RaycastServiceConfig _config;
        private Camera _defaultCamera;
        private Camera _camera;
        private RectTransform _view;
        private Canvas _viewCanvas;

        private Ray _debugRay;
        private bool _hasDebugRay;
        private bool _debugHit;
        private Vector3 _debugHitPoint;

        [Inject]
        public void Construct(RaycastServiceConfig config, Camera camera)
        {
            _config = config;
            _defaultCamera = camera;
            _camera = camera;

            if (_config.PointAction != null)
                _config.PointAction.action.Enable();
            else
                Debug.LogError($"[{nameof(RaycastService)}] Point Action is not assigned in {_config.name}", _config);
        }

        public void SwitchCamera(Camera camera, RectTransform view = null)
        {
            _camera = camera;
            _view = view;
            _viewCanvas = view != null ? view.GetComponentInParent<Canvas>().rootCanvas : null;
        }

        public void ResetCamera()
        {
            SwitchCamera(_defaultCamera);
        }

        public bool TryGetPointerRay(out Ray ray)
        {
            ray = default;

            if (_config == null || _config.PointAction == null || _camera == null || !Application.isFocused)
                return false;

            Vector2 screenPos = _config.PointAction.action.ReadValue<Vector2>();

            if (_view != null)
            {
                if (!TryGetViewPoint(screenPos, out Vector2 viewportPoint))
                    return false;

                ray = _camera.ViewportPointToRay(viewportPoint);
                return true;
            }

            // Курсор вне области, которую рисует камера (или вне окна игры)
            if (!_camera.pixelRect.Contains(screenPos))
                return false;

            ray = _camera.ScreenPointToRay(screenPos);
            return true;
        }

        // Переводит позицию курсора на экране в viewport-координаты (0..1) камеры,
        // чья RenderTexture показана в _view
        private bool TryGetViewPoint(Vector2 screenPos, out Vector2 viewportPoint)
        {
            viewportPoint = default;

            Camera uiCamera = _viewCanvas == null || _viewCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _viewCanvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_view, screenPos, uiCamera,
                    out Vector2 localPoint))
                return false;

            Rect rect = _view.rect;

            if (!rect.Contains(localPoint))
                return false;

            viewportPoint = Rect.PointToNormalized(rect, localPoint);
            return true;
        }

        public bool RaycastOnComponent<T>(out T component, LayerMask layerMask) where T : Component
        {
            component = null;

            if (!TryGetPointerRay(out Ray ray))
                return false;

            if (!Physics.Raycast(ray, out RaycastHit hit, _config.MaxRaycastDistance,
                    layerMask, QueryTriggerInteraction.Ignore))
                return false;

            component = hit.collider.GetComponentInParent<T>();
            return component != null;
        }

        // Луч считается в игровом цикле, а не в OnDrawGizmos: тот вызывается при перерисовке
        // любого окна (в т.ч. Scene), и позиция курсора там не относится к Game view
        private void Update()
        {
            _hasDebugRay = false;
            _debugHit = false;

            if (_config == null || !_config.ShowDebugRay || !TryGetPointerRay(out Ray ray))
                return;

            _hasDebugRay = true;
            _debugRay = ray;
            _debugHit = Physics.Raycast(ray, out RaycastHit hit, _config.MaxRaycastDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            _debugHitPoint = hit.point;
        }

        private void OnDestroy()
        {
            if (_config != null && _config.PointAction != null)
                _config.PointAction.action.Disable();
        }

        private void OnDrawGizmos()
        {
            if (_config == null || !_config.ShowDebugRay || !_hasDebugRay)
                return;

            Gizmos.color = _config.DebugRayColor;

            if (_debugHit)
            {
                Gizmos.DrawLine(_debugRay.origin, _debugHitPoint);
                Gizmos.DrawWireSphere(_debugHitPoint, 0.1f);
            }
            else
            {
                Gizmos.DrawRay(_debugRay.origin, _debugRay.direction * _config.MaxRaycastDistance);
            }
        }
    }
}
