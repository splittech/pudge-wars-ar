using Unity.Mathematics;

namespace Game.Core.Events
{
    public readonly struct HookThrown
    {
        public readonly uint OwnerId;
        public readonly float2 Origin;
        public readonly float2 Direction;
        public readonly double Time;

        public HookThrown(uint ownerId, float2 origin, float2 direction, double time)
        {
            OwnerId = ownerId;
            Origin = origin;
            Direction = direction;
            Time = time;
        }
    }
}
