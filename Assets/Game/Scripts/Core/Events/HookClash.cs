using Unity.Mathematics;

namespace Game.Core.Events
{
    public readonly struct HookClash
    {
        public readonly uint FirstOwnerId;
        public readonly uint SecondOwnerId;
        public readonly float2 Position;
        public readonly double Time;

        public HookClash(uint firstOwnerId, uint secondOwnerId, float2 position, double time)
        {
            FirstOwnerId = firstOwnerId;
            SecondOwnerId = secondOwnerId;
            Position = position;
            Time = time;
        }
    }
}
