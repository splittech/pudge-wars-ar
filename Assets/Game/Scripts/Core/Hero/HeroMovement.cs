using System;
using Game.Core.Geometry;
using Unity.Mathematics;

namespace Game.Core.Hero
{
    public static class HeroMovement
    {
        public static void Step(ref HeroState hero, float speed, Rect2 bounds, float dt)
        {
            if (!math.isfinite(dt) || dt < 0 || !math.isfinite(speed) || speed < 0)
                throw new ArgumentOutOfRangeException(nameof(dt));
            if (!hero.Alive || hero.IsPulled)
                return;
            // A released hero may be outside its half and must stay there until a new move command.
            if (math.all(hero.Pos == hero.MoveTarget))
                return;
            hero.MoveTarget = Geo2D.ClampToRect(hero.MoveTarget, bounds);
            float2 delta = hero.MoveTarget - hero.Pos;
            float distance = math.length(delta);
            hero.Pos = distance <= speed * dt ? hero.MoveTarget :
                hero.Pos + math.normalizesafe(delta) * (speed * dt);
        }
    }
}
