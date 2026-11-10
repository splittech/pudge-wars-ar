using Game.Core.Hook;
using UnityEngine;

namespace Game.Net
{
    public struct HookNetState
    {
        public HookPhase Phase;
        public Vector2 Origin;
        public Vector2 Dir;
        public double StartTime;
        public double TurnTime;
        public float TurnLength;
        public uint TargetNetId;
        public uint RequestId;
        public uint SessionEpoch;

        public static HookNetState FromSimulation(HookState hook, uint targetNetId) => new()
        {
            Phase = hook.Phase,
            Origin = new Vector2(hook.Origin.x, hook.Origin.y),
            Dir = new Vector2(hook.Dir.x, hook.Dir.y),
            StartTime = hook.StartTime,
            TurnTime = hook.TurnTime,
            TurnLength = hook.TurnLength,
            TargetNetId = targetNetId
        };

        public Vector2 EvaluateTip(double now, Vector2 owner, float speed, float range)
        {
            if (Phase == HookPhase.Idle)
                return owner;
            if (Phase == HookPhase.Extending)
                return Origin + Dir * Mathf.Clamp((float)(now - StartTime) * speed, 0, range);
            Vector2 turn = Origin + Dir * TurnLength;
            return Vector2.MoveTowards(turn, owner, Mathf.Max(0, (float)(now - TurnTime)) * speed);
        }
    }
}
