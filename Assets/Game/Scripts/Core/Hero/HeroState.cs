using Game.Core.Match;
using Unity.Mathematics;

namespace Game.Core.Hero
{
    public struct HeroState
    {
        public uint Id;
        public Team Team;
        public float2 Pos;
        public float2 MoveTarget;
        public float Radius;
        public int Hp;
        public bool Alive;
        public bool IsPulled;
        public double HookCooldownEnd;
    }
}
