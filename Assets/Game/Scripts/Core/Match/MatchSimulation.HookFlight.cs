using Game.Core.Events;
using Game.Core.Geometry;
using Game.Core.Hook;
using Unity.Mathematics;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private void RebaseHook(int index, float2 velocity, double time)
        {
            ref HookState hook = ref _hooks[index];
            hook.Origin = hook.Tip;
            hook.Length = 0;
            hook.StartTime = time;
            hook.Speed = math.length(velocity);
            hook.Dir = math.normalizesafe(velocity, hook.Dir);
        }

        private void StepFlyingHooks(float dt)
        {
            float remaining = dt;
            for (int iteration = 0; iteration < 128 && remaining > 0; iteration++)
            {
                float earliest = remaining;
                int first = -1, second = -1, kind = -1, axis = -1;
                for (int i = 0; i < _hooks.Length; i++)
                {
                    ref HookState hook = ref _hooks[i];
                    if (hook.Phase != HookPhase.Extending) continue;
                    hook.HalfBounds = math.max(0, _arena.Size * 0.5f - hook.Radius);
                    float2 velocity = hook.Dir * hook.Speed;
                    for (int a = 0; a < 2; a++)
                    {
                        if (math.abs(velocity[a]) < 1e-8f) continue;
                        float wall = math.sign(velocity[a]) * hook.HalfBounds[a];
                        float t = math.max(0, (wall - hook.Tip[a]) / velocity[a]);
                        if (t <= earliest && (first < 0 || t < earliest))
                        { earliest = t; first = i; kind = 0; axis = a; }
                    }
                    for (int j = i + 1; j < _hooks.Length; j++)
                    {
                        if (_hooks[j].Phase != HookPhase.Extending) continue;
                        float2 relative = velocity - _hooks[j].Dir * _hooks[j].Speed;
                        float2 normal = math.normalizesafe(_hooks[j].Tip - hook.Tip, math.normalizesafe(relative, new float2(1, 0)));
                        if (math.dot(relative, normal) <= 1e-6f) continue;
                        if (Geo2D.SweptCircle(hook.Tip, hook.Tip + relative * remaining, hook.Radius,
                            _hooks[j].Tip, _hooks[j].Radius, out float fraction))
                        {
                            float t = fraction * remaining;
                            if (t <= earliest && (first < 0 || t < earliest))
                            { earliest = t; first = i; second = j; kind = 1; }
                        }
                    }
                    for (int j = 0; j < _heroes.Length; j++)
                    {
                        if (j == i || !_heroes[j].Alive) continue;
                        if (Geo2D.SweptCircle(hook.Tip, hook.Tip + velocity * remaining, hook.Radius,
                            _heroes[j].Pos, _heroes[j].Radius, out float fraction))
                        {
                            float t = fraction * remaining;
                            if (t <= earliest && (first < 0 || t < earliest))
                            { earliest = t; first = i; second = j; kind = 2; }
                        }
                    }
                    if (hook.Speed > 0)
                    {
                        float t = math.max(0, (hook.Range - hook.Travelled) / hook.Speed);
                        if (t <= earliest && (first < 0 || t < earliest))
                        { earliest = t; first = i; kind = 3; }
                    }
                }
                for (int i = 0; i < _hooks.Length; i++)
                {
                    ref HookState hook = ref _hooks[i];
                    if (hook.Phase != HookPhase.Extending) continue;
                    float distance = hook.Speed * earliest;
                    hook.Tip += hook.Dir * distance;
                    hook.Length += distance;
                    hook.Travelled += distance;
                }
                remaining -= earliest;
                if (first < 0) break;
                double eventTime = Time - remaining;
                ref HookState active = ref _hooks[first];
                if (kind == 0)
                {
                    float2 velocity = active.Dir * active.Speed;
                    velocity[axis] = -velocity[axis];
                    RebaseHook(first, velocity, eventTime);
                }
                else if (kind == 1)
                {
                    float2 v1 = active.Dir * active.Speed;
                    float2 v2 = _hooks[second].Dir * _hooks[second].Speed;
                    float2 normal = math.normalizesafe(_hooks[second].Tip - active.Tip, math.normalizesafe(v1 - v2, new float2(1, 0)));
                    float impulse = math.dot(v1 - v2, normal);
                    RebaseHook(first, v1 - impulse * normal, eventTime);
                    RebaseHook(second, v2 + impulse * normal, eventTime);
                    _clashes.Add(new HookClash(active.OwnerId, _hooks[second].OwnerId,
                        (active.Tip + _hooks[second].Tip) * 0.5f, eventTime));
                }
                else if (kind == 2)
                {
                    ResolveHookHit(first, second, eventTime);
                }
                else HookSimulation.Turn(ref active, HookPhase.Retracting, eventTime);
            }
            for (int i = 0; i < _hooks.Length; i++)
                if (_hooks[i].Phase == HookPhase.Extending && _hooks[i].Travelled >= _hooks[i].Range - 1e-5f)
                {
                    _hooks[i].Travelled = _hooks[i].Range;
                    HookSimulation.Turn(ref _hooks[i], HookPhase.Retracting, Time);
                }
            // Stop safely if pathological contacts exhaust the event budget.
            if (remaining > 0)
                for (int i = 0; i < _hooks.Length; i++)
                    if (_hooks[i].Phase == HookPhase.Extending)
                        RebaseHook(i, _hooks[i].Dir * _hooks[i].Speed, Time);
        }
    }
}
