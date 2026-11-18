using System;
using Game.Core.Hook;
using UnityEngine;
using Unity.Mathematics;

namespace Game.Net
{
    public struct HookNetState : IEquatable<HookNetState>
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
        public HookPath Path;

        public bool Equals(HookNetState other)
        {
            return Phase == other.Phase && Origin.Equals(other.Origin) && Dir.Equals(other.Dir)
                && StartTime == other.StartTime && TurnTime == other.TurnTime && TurnLength == other.TurnLength
                && TargetNetId == other.TargetNetId && RequestId == other.RequestId && SessionEpoch == other.SessionEpoch
                && SegmentRange == other.SegmentRange && HalfBounds.Equals(other.HalfBounds) && Path.Equals(other.Path);
        }

        public static HookNetState FromSimulation(HookState hook, uint targetNetId)
        {
            var state = new HookNetState
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
            if (hook.Phase == HookPhase.Extending)
            {
                state.Path = hook.Path;
            }
            else if (hook.Phase != HookPhase.Idle)
            {
                state.Path = hook.Path.Prefix(math.clamp(hook.ReturnIndex + 1, 0, hook.Path.Count));
                state.Origin = new Vector2(hook.Tip.x, hook.Tip.y);
                state.StartTime = hook.SampleTime;
                state.TurnTime = hook.SampleTime;
                state.TurnLength = 0;
            }

            return state;
        }

        public Vector2 EvaluateTip(double now, Vector2 owner, float speed, float range)
        {
            if (Phase == HookPhase.Idle)
            {
                return owner;
            }

            if (Phase == HookPhase.Extending)
            {
                Vector2 tip = Origin + Dir * Mathf.Clamp((float)(now - StartTime) * speed, 0,
                    SegmentRange > 0 ? SegmentRange : range);
                if (HalfBounds.x > 0 && HalfBounds.y > 0)
                {
                    tip = Vector2.Min(HalfBounds, Vector2.Max(-HalfBounds, tip));
                }

                return tip;
            }
            Vector2 turn = Origin + Dir * TurnLength;
            float distance = Mathf.Max(0, (float)(now - TurnTime)) * speed;
            float2 result = Path.EvaluateReturn(
                new float2(turn.x, turn.y),
                new float2(owner.x, owner.y),
                distance,
                out _);
            return new Vector2(result.x, result.y);
        }

        public int RemainingPathCount(double now, Vector2 owner, float speed)
        {
            if (Phase == HookPhase.Extending)
            {
                return Path.Count;
            }

            Vector2 turn = Origin + Dir * TurnLength;
            Path.EvaluateReturn(
                new float2(turn.x, turn.y),
                new float2(owner.x, owner.y),
                Mathf.Max(0, (float)(now - TurnTime)) * speed,
                out int index);
            return index + 1;
        }
    }
}
