using Game.Core.Configs;
using Game.Core.Hero;
using Unity.Mathematics;

namespace Game.Core.Hook
{
    public static class HookSimulation
    {
        public static bool TryThrow(ref HookState hook, HeroState owner, float2 direction, double now)
        {
            if (!owner.Alive || owner.IsPulled || hook.Phase != HookPhase.Idle ||
                now < owner.HookCooldownEnd || !math.all(math.isfinite(direction)) ||
                math.lengthsq(direction) < 1e-12f || !math.isfinite(math.lengthsq(direction)))
            {
                return false;
            }

            hook = new HookState
            {
                OwnerId = owner.Id,
                Phase = HookPhase.Extending,
                Origin = owner.Pos,
                Tip = owner.Pos,
                Dir = math.normalize(direction),
                StartTime = now,
                Speed = owner.HookSpeed,
                Range = owner.HookRange,
                Radius = owner.HookRadius,
                Damage = owner.HookDamage,
                ReturnIndex = -1,
                SampleTime = now
            };
            hook.Path.TryAdd(owner.Pos);
            return true;
        }

        public static void Turn(ref HookState hook, HookPhase phase, double now)
        {
            if (hook.Phase == HookPhase.Extending || hook.Path.Count == 0)
            {
                hook.ReturnIndex = hook.Path.Count - 1;
            }

            hook.Phase = phase;
            hook.TurnTime = now;
            hook.TurnLength = hook.Length;
            hook.SampleTime = now;
        }

        public static bool Return(ref HookState hook, HeroState owner, HookConfigData config, float dt)
        {
            HookPath remaining = hook.Path.Prefix(math.clamp(hook.ReturnIndex + 1, 0, hook.Path.Count));
            hook.Tip = remaining.EvaluateReturn(hook.Tip, owner.Pos, config.Speed * dt, out hook.ReturnIndex);
            bool finalLeg = hook.ReturnIndex < 0;
            if (hook.ReturnIndex == 0 && math.distancesq(hook.Path.Get(0), owner.Pos) < 1e-10f)
            {
                finalLeg = true;
            }

            if (finalLeg && math.distance(owner.Pos, hook.Tip) <= owner.Radius + config.Radius)
            {
                hook.Tip = owner.Pos;
                hook.ReturnIndex = -1;
                return true;
            }

            return false;
        }
    }
}
