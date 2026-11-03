using Alchemy.Inspector;
using UnityEngine;

namespace Game.Core.Configs
{
    [CreateAssetMenu(fileName = "Hook Config", menuName = "Game/Configs/Hook Config", order = 0)]
    public sealed class HookConfig : ScriptableObject
    {
        [BoxGroup("Hook")]
        [SerializeField] private float _range = 12f;

        [BoxGroup("Hook")]
        [SerializeField] private float _speed = 20f;

        [BoxGroup("Hook")]
        [SerializeField] private float _radius = 0.35f;

        [BoxGroup("Hook")]
        [SerializeField] private int _damage = 175;

        [BoxGroup("Hook")]
        [SerializeField] private float _cooldown = 3f;

        [BoxGroup("Hook")]
        [SerializeField] private float _rangePerLevel = 1.5f;

        [BoxGroup("Hook")]
        [SerializeField] private float _speedPerLevel = 4f;

        [BoxGroup("Hook")]
        [SerializeField] private float _radiusPerLevel = 0.08f;

        [BoxGroup("Hook")]
        [SerializeField] private int _damagePerLevel = 75;

        [BoxGroup("Hook")]
        [SerializeField] private int _maxLevel = 4;

        public HookConfigData ToData()
        {
            return new HookConfigData(_range, _speed, _radius, _damage, _cooldown, _rangePerLevel, _speedPerLevel, _radiusPerLevel, _damagePerLevel, _maxLevel);
        }
    }
}
