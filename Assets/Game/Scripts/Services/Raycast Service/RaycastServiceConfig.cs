using Alchemy.Inspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Services.RaycastService
{
    [CreateAssetMenu(fileName = "Raycast Service Config", menuName = "Game/Configs/Raycast Service Config", order = 0)]
    public class RaycastServiceConfig : ScriptableObject
    {
        [BoxGroup("General")]
        [SerializeField] private float _maxRaycastDistance = 100f;
        [BoxGroup("General")]
        [SerializeField] private InputActionReference _pointAction;

        [BoxGroup("Debug")]
        [SerializeField] private bool _showDebugRay;
        [BoxGroup("Debug")]
        [SerializeField] private Color _debugRayColor = Color.red;

        public float MaxRaycastDistance => _maxRaycastDistance;
        public bool ShowDebugRay => _showDebugRay;
        public Color DebugRayColor => _debugRayColor;
        public InputActionReference PointAction => _pointAction;
    }
}