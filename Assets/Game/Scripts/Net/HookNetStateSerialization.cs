using Game.Core.Hook;
using Mirror;

namespace Game.Net
{
    public static class HookNetStateSerialization
    {
        public static void WriteHookNetState(this NetworkWriter writer, HookNetState value)
        {
            writer.WriteByte((byte)value.Phase);
            writer.WriteVector2(value.Origin);
            writer.WriteVector2(value.Dir);
            writer.WriteDouble(value.StartTime);
            writer.WriteDouble(value.TurnTime);
            writer.WriteFloat(value.TurnLength);
            writer.WriteUInt(value.TargetNetId);
            writer.WriteUInt(value.RequestId);
            writer.WriteUInt(value.SessionEpoch);
        }

        public static HookNetState ReadHookNetState(this NetworkReader reader) => new()
        {
            Phase = (HookPhase)reader.ReadByte(),
            Origin = reader.ReadVector2(),
            Dir = reader.ReadVector2(),
            StartTime = reader.ReadDouble(),
            TurnTime = reader.ReadDouble(),
            TurnLength = reader.ReadFloat(),
            TargetNetId = reader.ReadUInt(),
            RequestId = reader.ReadUInt(),
            SessionEpoch = reader.ReadUInt()
        };
    }
}
