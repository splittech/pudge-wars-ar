using Alchemy.Inspector;
using UnityEngine;

namespace Game.Core.Configs
{
    [CreateAssetMenu(fileName = "Input Config", menuName = "Game/Configs/Input Config", order = 0)]
    public class InputConfig : ScriptableObject
    {
        [BoxGroup("Gestures")]
        [Tooltip("Maximum touch duration in seconds that still counts as a tap.")]
        [SerializeField] private float _tapMaxTime = 0.3f;

        [BoxGroup("Gestures")]
        [Tooltip("Finger movement in dp (pixels at 160 dpi) after which a touch becomes a drag.")]
        [SerializeField] private float _dragThreshold = 10f;

        [BoxGroup("Aiming")]
        [Tooltip("A drag starting within this distance from the own hero aims the hook, in arena units (FR-IN-02).")]
        [SerializeField] private float _heroGrabRadius = 1.5f;

        [BoxGroup("Aiming")]
        [Tooltip("Aim assist cone half-angle in degrees (FR-IN-05).")]
        [SerializeField] private float _aimAssistAngle = 4f;

        [BoxGroup("Arena Scale")]
        [Tooltip("Minimum ArenaRoot scale, meters per arena unit.")]
        [SerializeField] private float _arenaScaleMin = 0.02f;

        [BoxGroup("Arena Scale")]
        [Tooltip("Maximum ArenaRoot scale, meters per arena unit.")]
        [SerializeField] private float _arenaScaleMax = 0.08f;

        public InputConfigData ToData()
        {
            return new InputConfigData(
                _tapMaxTime, _dragThreshold, _heroGrabRadius, _aimAssistAngle, _arenaScaleMin, _arenaScaleMax);
        }
    }
}
