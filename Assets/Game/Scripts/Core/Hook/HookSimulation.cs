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
                return false;
            hook = new HookState
            {
                OwnerId = owner.Id, Phase = HookPhase.Extending, Origin = owner.Pos,
                Tip = owner.Pos, Dir = math.normalize(direction), StartTime = now
            };
            return true;
        }

        public static void Turn(ref HookState hook, HookPhase phase, double now)
        {
            hook.Phase = phase;
            hook.TurnTime = now;
            hook.TurnLength = hook.Length;
        }

        public static bool Return(ref HookState hook, HeroState owner, HookConfigData config, float dt)
        {
            float2 delta = owner.Pos - hook.Tip;
            float distance = math.length(delta);
            if (distance <= config.Speed * dt + owner.Radius + config.Radius)
            {
                hook.Tip = owner.Pos;
                return true;
            }
            hook.Tip += math.normalizesafe(delta) * config.Speed * dt;
            return false;
        }
    }
}
