using Game.Core.Hook;
using Mirror;

namespace Game.Net
{
    public static class HookNetStateSerialization
    {
        public static void WriteHookNetState(this NetworkWriter writer, HookNetState value)
        {
            Validate(value);
            writer.WriteByte((byte)value.Phase);
            writer.WriteVector2(value.Origin);
            writer.WriteVector2(value.Dir);
            writer.WriteDouble(value.StartTime);
            writer.WriteDouble(value.TurnTime);
            writer.WriteFloat(value.TurnLength);
            writer.WriteUInt(value.TargetNetId);
            writer.WriteUInt(value.RequestId);
            writer.WriteUInt(value.SessionEpoch);
            writer.WriteFloat(value.SegmentRange);
            writer.WriteVector2(value.HalfBounds);
            writer.WriteByte((byte)value.Path.Count);
            for (int i = 0; i < value.Path.Count; i++)
            {
                var point = value.Path.Get(i);
                writer.WriteVector2(new UnityEngine.Vector2(point.x, point.y));
            }
        }

        public static HookNetState ReadHookNetState(this NetworkReader reader)
        {
            var value = new HookNetState
            {
                Phase = (HookPhase)reader.ReadByte(),
                Origin = reader.ReadVector2(),
                Dir = reader.ReadVector2(),
                StartTime = reader.ReadDouble(),
                TurnTime = reader.ReadDouble(),
                TurnLength = reader.ReadFloat(),
                TargetNetId = reader.ReadUInt(),
                RequestId = reader.ReadUInt(),
                SessionEpoch = reader.ReadUInt(),
                SegmentRange = reader.ReadFloat(),
                HalfBounds = reader.ReadVector2()
            };
            byte count = reader.ReadByte();
            SessionSerialization.Require(count <= HookPath.Capacity && reader.Remaining >= count * 8);
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Vector2 point = reader.ReadVector2();
                SessionSerialization.Require(value.Path.TryAdd(new Unity.Mathematics.float2(point.x, point.y)));
            }

            Validate(value);
            return value;
        }

        private static void Validate(HookNetState value)
        {
            SessionSerialization.Require(value.Phase <= HookPhase.Pulling
                && SessionSerialization.Finite(value.Origin) && SessionSerialization.Finite(value.Dir)
                && SessionSerialization.TimeValid(value.StartTime) && SessionSerialization.TimeValid(value.TurnTime)
                && float.IsFinite(value.SegmentRange) && value.SegmentRange >= 0
                && SessionSerialization.Finite(value.HalfBounds) && value.HalfBounds.x >= 0 && value.HalfBounds.y >= 0
                && float.IsFinite(value.TurnLength) && value.TurnLength >= 0);
            if (value.Phase != HookPhase.Idle)
            {
                SessionSerialization.Require(UnityEngine.Mathf.Abs(value.Dir.sqrMagnitude - 1) < 0.001f);
            }
        }
    }
}
