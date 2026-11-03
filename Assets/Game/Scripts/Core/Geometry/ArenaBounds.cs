using System;
using Game.Core.Configs;
using Game.Core.Match;
using Unity.Mathematics;

namespace Game.Core.Geometry
{
    public static class ArenaBounds
    {
        public static Rect2 ForTeam(ArenaConfigData arena, Team team, float heroRadius)
        {
            if (!math.all(math.isfinite(arena.Size)) || math.any(arena.Size <= 0f))
                throw new ArgumentException("Arena size must be finite and positive.", nameof(arena));
            if (!math.isfinite(arena.RiverWidth) || arena.RiverWidth < 0f || arena.RiverWidth >= arena.Size.x)
                throw new ArgumentException("River must fit inside the arena.", nameof(arena));
            if (!math.isfinite(heroRadius) || heroRadius < 0f)
                throw new ArgumentOutOfRangeException(nameof(heroRadius));
            if (team != Team.A && team != Team.B)
                throw new ArgumentOutOfRangeException(nameof(team));

            float2 halfSize = arena.Size * 0.5f;
            float riverEdge = arena.RiverWidth * 0.5f;
            float minX = team == Team.A ? -halfSize.x + heroRadius : riverEdge + heroRadius;
            float maxX = team == Team.A ? -riverEdge - heroRadius : halfSize.x - heroRadius;
            return new Rect2(new float2(minX, -halfSize.y + heroRadius),
                new float2(maxX, halfSize.y - heroRadius));
        }
    }
}
