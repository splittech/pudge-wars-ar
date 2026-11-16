using Unity.Mathematics;

namespace Game.Core.Hook
{
    public struct HookState
    {
        public uint OwnerId;
        public HookPhase Phase;
        public float2 Origin;
        public float2 Dir;
        public float Length;
        public float Travelled;
        public float2 HalfBounds;
        public float2 Tip;
        public uint TargetId;
        public double TurnTime;
        public float TurnLength;
        public double StartTime;
        public float Speed;
        public float Range;
        public float Radius;
        public int Damage;
    }
}
