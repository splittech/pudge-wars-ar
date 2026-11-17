using Unity.Mathematics;
using UnityEngine;

namespace Game.Input.Routing
{
    public sealed class FlatArenaSpace : MonoBehaviour, IArenaSpace
    {
        [SerializeField] private Transform _arenaRoot;
        [SerializeField] private Camera _camera;
        public Camera ActiveCamera => _camera;
        public bool InputEnabled { get; set; } = true;
        public void SetCamera(Camera camera) => _camera = camera;

        public bool TryScreenToArena(Vector2 screenPoint, out float2 arenaPoint)
        {
            arenaPoint = default;
            if (!InputEnabled || _camera == null || !_arenaRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            var plane = new Plane(_arenaRoot.up, _arenaRoot.position);
            Ray ray = _camera.ScreenPointToRay(screenPoint);
            arenaPoint = default;
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            Vector3 point = _arenaRoot.InverseTransformPoint(ray.GetPoint(distance));
            arenaPoint = new float2(point.x, point.z);
            return true;
        }

        public Vector3 ArenaToWorld(float2 point) => _arenaRoot.TransformPoint(new Vector3(point.x, 0, point.y));
    }
}
