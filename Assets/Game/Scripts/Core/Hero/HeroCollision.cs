using System.Collections.Generic;
using Game.Core.Geometry;
using Unity.Mathematics;

namespace Game.Core.Hero
{
    public static class HeroCollision
    {
        public static float2 Move(HeroState hero, float2 destination, IReadOnlyList<HeroState> heroes)
        {
            if (!hero.Alive || heroes == null)
            {
                return destination;
            }

            float2 delta = destination - hero.Pos;
            float fraction = 1;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroState other = heroes[i];
                if (!other.Alive || other.Id == hero.Id)
                {
                    continue;
                }

                float2 offset = hero.Pos - other.Pos;
                float radius = hero.Radius + other.Radius;
                // Contacts must allow separating and tangential movement.
                if (math.dot(offset, delta) >= 0)
                {
                    continue;
                }

                if (math.lengthsq(offset) <= radius * radius)
                {
                    fraction = 0;
                    continue;
                }
                if (Geo2D.SweptCircle(hero.Pos, destination, hero.Radius, other.Pos, other.Radius, out float t))
                {
                    fraction = math.min(fraction, t);
                }
            }
            return hero.Pos + delta * fraction;
        }
    }
}
