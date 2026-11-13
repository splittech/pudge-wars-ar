using UnityEngine;

namespace Game.Net
{
    public struct MovementBatch
    {
        public byte Count;
        public MovementFrame First;
        public MovementFrame Second;
        public MovementFrame Third;
        public MovementFrame Fourth;
        public MovementFrame Get(int index) => index switch
        {
            0 => First, 1 => Second, 2 => Third, 3 => Fourth,
            _ => throw new System.ArgumentOutOfRangeException(nameof(index))
        };
        public void Add(MovementFrame frame)
        {
            switch (Count++)
            {
                case 0: First = frame; break;
                case 1: Second = frame; break;
                case 2: Third = frame; break;
                case 3: Fourth = frame; break;
                default: throw new System.InvalidOperationException("Movement batch is full.");
            }
        }
    }

    public struct MovementFrame
    {
        public uint Sequence;
        public uint ClientTick;
        public bool HasMoveTarget;
        public Vector2 Target;
        public uint SessionEpoch;
    }

    public struct MovementSnapshot
    {
        public uint ServerTick;
        public double ServerTime;
        public uint AckSequence;
        public uint SessionEpoch;
        public Vector2 Pos;
        public Vector2 MoveTarget;
        public float MoveSpeed;
        public bool Alive;
        public bool IsPulled;
    }
}
