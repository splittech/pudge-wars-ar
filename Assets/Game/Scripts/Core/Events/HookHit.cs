using Unity.Mathematics;

namespace Game.Core.Events
{
    public readonly struct HookHit
    {
        public readonly uint OwnerId;
        public readonly uint TargetId;
        public readonly float2 Position;
        public readonly double Time;

        public HookHit(uint ownerId, uint targetId, float2 position, double time)
        {
            OwnerId = ownerId;
            TargetId = targetId;
            Position = position;
            Time = time;
        }
    }
}
