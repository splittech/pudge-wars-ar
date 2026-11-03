using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Game.Core.Configs
{
    public readonly struct ArenaConfigData
    {
        public readonly float2 Size;
        public readonly float RiverWidth;
        public readonly IReadOnlyList<float2> TeamASpawnPoints;
        public readonly IReadOnlyList<float2> TeamBSpawnPoints;

        public ArenaConfigData(float2 size, float riverWidth, float2[] teamASpawnPoints, float2[] teamBSpawnPoints)
        {
            Size = size;
            RiverWidth = riverWidth;
            TeamASpawnPoints = Array.AsReadOnly((float2[])teamASpawnPoints.Clone());
            TeamBSpawnPoints = Array.AsReadOnly((float2[])teamBSpawnPoints.Clone());
        }
    }
}
