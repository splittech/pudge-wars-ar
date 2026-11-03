using Alchemy.Inspector;
using UnityEngine;

namespace Game.Core.Configs
{
    [CreateAssetMenu(fileName = "Hero Config", menuName = "Game/Configs/Hero Config", order = 0)]
    public sealed class HeroConfig : ScriptableObject
    {
        [BoxGroup("Hero")]
        [SerializeField] private int _maxHp = 1000;

        [BoxGroup("Hero")]
        [SerializeField] private float _moveSpeed = 3.2f;

        [BoxGroup("Hero")]
        [SerializeField] private float _radius = 0.5f;

        [BoxGroup("Hero")]
        [SerializeField] private float _respawnTime = 4f;

        public HeroConfigData ToData()
        {
            return new HeroConfigData(_maxHp, _moveSpeed, _radius, _respawnTime);
        }
    }
}
