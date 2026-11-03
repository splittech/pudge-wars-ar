using Alchemy.Inspector;
using UnityEngine;
using Unity.Mathematics;

namespace Game.Core.Configs
{
    [CreateAssetMenu(fileName = "Arena Config", menuName = "Game/Configs/Arena Config", order = 0)]
    public sealed class ArenaConfig : ScriptableObject
    {
        [BoxGroup("Arena")]
        [SerializeField] private float2 _size = new float2(24f, 14f);

        [BoxGroup("Arena")]
        [SerializeField] private float _riverWidth = 3.5f;

        [BoxGroup("Arena")]
        [SerializeField] private float2[] _teamASpawnPoints = new[] { new float2(-8f, -4f), new float2(-8f, 0f), new float2(-8f, 4f) };

        [BoxGroup("Arena")]
        [SerializeField] private float2[] _teamBSpawnPoints = new[] { new float2(8f, -4f), new float2(8f, 0f), new float2(8f, 4f) };

        public ArenaConfigData ToData()
        {
            return new ArenaConfigData(_size, _riverWidth, _teamASpawnPoints, _teamBSpawnPoints);
        }
    }
}
