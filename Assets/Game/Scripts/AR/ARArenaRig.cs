using Game.Core.Configs;
using Game.Input.Gestures;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Game.AR
{
    public sealed class ARArenaRig : MonoBehaviour
    {
        [SerializeField] private ARSession _session;
        [SerializeField] private GameObject _origin;
        [SerializeField] private Camera _camera;
        [SerializeField] private ARPlaneManager _planes;
        [SerializeField] private ARRaycastManager _raycasts;
        [SerializeField] private ARAnchorManager _anchors;

        public Camera Camera => _camera;

        public void SetActive(bool active)
        {
            if (_origin != null)
            {
                _origin.SetActive(active);
            }

            if (_session != null)
            {
                // Availability is checked before the session is enabled.
                _session.enabled = false;
                _session.gameObject.SetActive(active);
            }
        }

        public ARArenaAnchorProvider CreateProvider(
            Transform root,
            ITouchGestures gestures,
            InputConfigData input,
            Vector2 size)
        {
            return new ARArenaAnchorProvider(_session, _planes, _raycasts, _anchors, _camera, root, gestures, input, size);
        }
    }
}
