using Game.Core.Geometry;
using Game.Core.Hook;
using Unity.Mathematics;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private float2 ClampCollisionPosition(int index, float2 position)
        {
            var hero = _heroes[index];
            var teamBounds = ArenaBounds.ForPosition(_arena, hero.Pos, hero.Radius);
            bool inOwnHalf = math.all(hero.Pos >= teamBounds.Min) && math.all(hero.Pos <= teamBounds.Max);
            var bounds = inOwnHalf && !hero.IsPulled ? teamBounds :
                new Rect2(-_arena.Size * 0.5f + hero.Radius, _arena.Size * 0.5f - hero.Radius);
            return Geo2D.ClampToRect(position, bounds);
        }

        private void ResolveHeroOverlaps()
        {
            for (int pass = 0; pass < 32; pass++)
            {
                bool changed = false;
                for (int i = 0; i < _heroes.Length; i++)
                {
                    for (int j = i + 1; j < _heroes.Length; j++)
                    {
                        if (!_heroes[i].Alive || !_heroes[j].Alive)
                        {
                            continue;
                        }

                        float2 delta = _heroes[j].Pos - _heroes[i].Pos;
                        float distance = math.length(delta);
                        float overlap = _heroes[i].Radius + _heroes[j].Radius - distance;
                        if (overlap <= 1e-5f)
                        {
                            continue;
                        }

                        float2 normal = math.normalizesafe(delta,
                            new float2(_heroes[i].Id < _heroes[j].Id ? 1 : -1, 0));
                        float2 first = _heroes[i].Pos, second = _heroes[j].Pos;
                        bool firstIdle = math.all(_heroes[i].MoveTarget == first);
                        bool secondIdle = math.all(_heroes[j].MoveTarget == second);
                        _heroes[i].Pos = ClampCollisionPosition(i, first - normal * (overlap * 0.5f + 1e-5f));
                        _heroes[j].Pos = ClampCollisionPosition(j, second + normal * (overlap * 0.5f + 1e-5f));
                        if (firstIdle)
                        {
                            _heroes[i].MoveTarget = _heroes[i].Pos;
                        }

                        if (secondIdle)
                        {
                            _heroes[j].MoveTarget = _heroes[j].Pos;
                        }

                        changed |= math.any(first != _heroes[i].Pos) || math.any(second != _heroes[j].Pos);
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
            for (int i = 0; i < _hooks.Length; i++)
            {
                if (_hooks[i].Phase == HookPhase.Pulling)
                {
                    int target = Find(_hooks[i].TargetId);
                    if (target >= 0 && math.any(_hooks[i].Tip != _heroes[target].Pos))
                    {
                        _hooks[i].Tip = _heroes[target].Pos;
                        _hooks[i].Origin = _hooks[i].Tip;
                        _hooks[i].Length = 0;
                        _hooks[i].TurnLength = 0;
                        _hooks[i].TurnTime = Time;
                        _hooks[i].StartTime = Time;
                        _hooks[i].SampleTime = Time;
                    }
                }
            }
        }
    }
}
