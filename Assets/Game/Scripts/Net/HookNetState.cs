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
        public float SegmentRange;
        public Vector2 HalfBounds;

        public static HookNetState FromSimulation(HookState hook, uint targetNetId) => new()
        {
            Phase = hook.Phase,
            Origin = new Vector2(hook.Origin.x, hook.Origin.y),
            Dir = new Vector2(hook.Dir.x, hook.Dir.y),
            StartTime = hook.StartTime,
            TurnTime = hook.TurnTime,
            TurnLength = hook.TurnLength,
            SegmentRange = Mathf.Max(0, hook.Range - hook.Travelled + hook.Length),
            HalfBounds = new Vector2(hook.HalfBounds.x, hook.HalfBounds.y),
            TargetNetId = targetNetId
        };

        public Vector2 EvaluateTip(double now, Vector2 owner, float speed, float range)
        {
            if (Phase == HookPhase.Idle)
                return owner;
            if (Phase == HookPhase.Extending)
                {
                Vector2 tip = Origin + Dir * Mathf.Clamp((float)(now - StartTime) * speed, 0,
                    SegmentRange > 0 ? SegmentRange : range);
                if (HalfBounds.x > 0 && HalfBounds.y > 0)
                    tip = Vector2.Min(HalfBounds, Vector2.Max(-HalfBounds, tip));
                return tip;
            }
            Vector2 turn = Origin + Dir * TurnLength;
            return Vector2.MoveTowards(turn, owner, Mathf.Max(0, (float)(now - TurnTime)) * speed);
        }
    }
}
